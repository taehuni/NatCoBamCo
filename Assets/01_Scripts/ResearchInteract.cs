using UnityEngine;

[DefaultExecutionOrder(-70)]
public class ResearchInteract : MonoBehaviour, IInteractionTarget
{
    public ResearchUI researchUI;
    public float detectRange = 4f;
    private PlayerController player;
    private PlayerInteractUI playerUI;

    public KeyCode InteractionKey => KeyCode.E;
    public bool CanBeginInteraction => researchUI != null && !researchUI.IsOpen;
    public bool InteractionInProgress => false;
    public bool IsPlayerInInteractionRange(PlayerController candidate) =>
        InteractionSelection.IsInRange(this, candidate, detectRange, ~0);

    void OnEnable() => InteractionSelection.Register(this);

    void OnDisable()
    {
        if (playerUI != null) playerUI.HideButton(this);
        InteractionSelection.Unregister(this);
        player = null;
        playerUI = null;
    }

    void Awake()
    {
        ResolveResearchUI();
    }

    void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            playerUI = player != null ? player.GetComponent<PlayerInteractUI>() : null;
        }
        ResolveResearchUI();
        bool inRange = player != null && IsPlayerInInteractionRange(player);
        if (playerUI != null)
        {
            if (inRange && CanBeginInteraction) playerUI.ShowButton("연구소(E)", this);
            else playerUI.HideButton(this);
        }
        if (inRange && Input.GetKeyDown(KeyCode.E)) Interact();
    }

    public void Interact()
    {
        if (InteractionSelection.TryBegin(this, player)) researchUI.OpenUI();
    }

    void ResolveResearchUI()
    {
        if (researchUI == null)
        {
            researchUI = FindFirstObjectByType<ResearchUI>(FindObjectsInactive.Include);
        }
    }
}
