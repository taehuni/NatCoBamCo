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
            if (playerUI != null)
            {
                playerUI.HideButton();
                playerUI = null;
            }
        }
    }

    void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty.");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
