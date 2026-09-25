using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour, IInteractionTarget, IInteractable
{
    public float detectRange = 2f;
    public LayerMask playerLayer;
    public string targetSceneName;
    public KeyCode interactKey = KeyCode.E;

    private bool playerInRange;
    private PlayerInteractUI playerUI;

    private PlayerController interactionPlayer;
    public KeyCode InteractionKey => interactKey;
    private bool IsDefenseExitBlocked => gameObject.scene.name == "01_Main" &&
        targetSceneName == "02_Collection" && GameManager.Instance != null &&
        (GameManager.Instance.currentPhase == GameManager.GamePhase.NightStart ||
         GameManager.Instance.currentPhase == GameManager.GamePhase.Defense);
    public bool CanBeginInteraction => !IsDefenseExitBlocked &&
        !string.IsNullOrEmpty(targetSceneName) && UnityEngine.Application.CanStreamedLevelBeLoaded(targetSceneName);
    public bool InteractionInProgress => false;
    public bool IsPlayerInInteractionRange(PlayerController player) =>
        InteractionSelection.IsInRange(this, player, detectRange, playerLayer);

    void OnEnable() => InteractionSelection.Register(this);

    void OnDisable()
    {
        ClearPlayerInteraction();
        InteractionSelection.Unregister(this);
    }

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

    public void Interact()
    {
        if (!isActiveAndEnabled || !playerInRange ||
            !InteractionSelection.TryBegin(this, interactionPlayer)) return;
        LoadTargetScene();
    }

    void CheckPlayerInRange()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        playerInRange = players.Length > 0;
        interactionPlayer = playerInRange ? players[0].GetComponentInParent<PlayerController>() : null;

        if (playerInRange)
        {
            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();

            if (playerUI != null)
            {
                playerUI.ShowButton(IsDefenseExitBlocked
                    ? "방어 중에는 이동할 수 없습니다"
                    : $"전송하기({interactKey})", this);
            }
        }
        else
        {
            ClearPlayerInteraction();
        }
    }

    void ClearPlayerInteraction()
    {
        if (playerUI != null) playerUI.HideButton(this);
        playerUI = null;
        playerInRange = false;
        interactionPlayer = null;
    }

    void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty.");
            return;
        }

        if (!CanBeginInteraction) return;
        ClearPlayerInteraction();
        SceneManager.LoadScene(targetSceneName);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
