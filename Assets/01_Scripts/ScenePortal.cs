using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour, IInteractionTarget
{
    public float detectRange = 2f;
    public LayerMask playerLayer;
    public string targetSceneName;

    private bool playerInRange;
    private PlayerInteractUI playerUI;

    private PlayerController interactionPlayer;
    public KeyCode InteractionKey => KeyCode.E;
    public bool CanBeginInteraction => !string.IsNullOrEmpty(targetSceneName) && UnityEngine.Application.CanStreamedLevelBeLoaded(targetSceneName);
    public bool InteractionInProgress => false;
    public bool IsPlayerInInteractionRange(PlayerController player) =>
        InteractionSelection.IsInRange(this, player, detectRange, playerLayer);

    void OnEnable() => InteractionSelection.Register(this);

    void OnDisable()
    {

        if (playerUI != null) playerUI.HideButton(this);
        InteractionSelection.Unregister(this);
        interactionPlayer = null;
    }

    void Start()
    {
    }

    void Update()
    {
        玩家靠近检测();

        if (playerInRange && Input.GetKeyDown(KeyCode.E) &&
            InteractionSelection.TryBegin(this, interactionPlayer))
        {
            LoadTargetScene();
        }
    }

    void 玩家靠近检测()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        playerInRange = players.Length > 0;
        interactionPlayer = playerInRange ? players[0].GetComponentInParent<PlayerController>() : null;

        if (playerInRange)
        {
            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();

            if (playerUI != null)
            {
                playerUI.ShowButton("전송하기(E)", this);
            }
        }
        else
        {
            if (playerUI != null)
            {
                playerUI.HideButton(this);
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
