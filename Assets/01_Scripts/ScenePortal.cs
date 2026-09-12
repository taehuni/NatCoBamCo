using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour, IInteractable
{
    public float detectRange = 2f;
    public LayerMask playerLayer;
    public string targetSceneName;

    // 可在 Inspector 中设置，也可以由后续的玩家按键设置系统修改。
    // Inspector에서 설정하거나 추후 플레이어 키 설정 시스템에서 변경할 수 있다.
    public KeyCode interactKey = KeyCode.E;

    private bool playerInRange;
    private PlayerInteractUI playerUI;

    void Start()
    {
    }

    void Update()
    {
        CheckPlayerInRange();

        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    // 统一互动入口：玩家在检测范围内时才允许传送。
    // 공통 상호작용 입구: 플레이어가 감지 범위 안에 있을 때만 전송한다.
    public void Interact()
    {
        if (!playerInRange)
        {
            return;
        }

        LoadTargetScene();
    }

    void CheckPlayerInRange()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        playerInRange = players.Length > 0;

        if (playerInRange)
        {
            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();

            if (playerUI != null)
            {
                playerUI.ShowButton($"전송하기({interactKey})");
            }
        }
        else
        {
            ClearPlayerInteraction();
        }
    }

    // 玩家 UI 会跨场景保留，离开或关闭传送点时主动清除提示和交互状态。
    // 플레이어 UI는 씬 전환 후에도 유지되므로 포털에서 벗어나거나 포털이 비활성화되면 안내와 상호작용 상태를 정리한다.
    void ClearPlayerInteraction()
    {
        if (playerUI != null)
        {
            playerUI.HideButton();
        }

        playerUI = null;
        playerInRange = false;
    }

    void OnDisable()
    {
        ClearPlayerInteraction();
    }

    void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty.");
            return;
        }

        // 切场景不会再执行旧传送点的离开检测，因此先关闭玩家身上的提示。
        // 씬을 전환하면 기존 포털의 범위 이탈 검사가 실행되지 않으므로 플레이어의 안내를 먼저 닫는다.
        ClearPlayerInteraction();
        SceneManager.LoadScene(targetSceneName);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
