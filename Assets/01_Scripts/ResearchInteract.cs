using UnityEngine;

public class ResearchInteract : MonoBehaviour, IInteractionTarget
{
    public ResearchUI researchUI;
    public string playerTag = "Player";

    private bool isPlayerNear = false;

    private PlayerController interactionPlayer;
    private PlayerInteractUI playerUI;
    public KeyCode InteractionKey => KeyCode.E;
    public bool CanBeginInteraction => researchUI != null && researchUI.researchPanel != null;
    public bool InteractionInProgress => false;
    public bool IsPlayerInInteractionRange(PlayerController player) =>
        isPlayerNear && interactionPlayer != null && interactionPlayer == player;

    void OnEnable() => InteractionSelection.Register(this);

    void OnDisable()
    {
        if (playerUI != null) playerUI.HideButton(this);
        InteractionSelection.Unregister(this);
    }

    void Update()
    {
        if (isPlayerNear && playerUI != null) playerUI.ShowButton("연구소(E)", this);
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E) &&
            InteractionSelection.TryBegin(this, interactionPlayer))
        {
            Debug.Log("E키 입력됨 - 연구소 UI 열기 시도");

            if (researchUI != null)
            {
                researchUI.OpenUI();
            }
            else
            {
                Debug.LogError("ResearchUI가 연결되지 않았습니다.");
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter: " + other.name);

        if (other.CompareTag(playerTag))
        {
            Debug.Log("플레이어가 연구소 범위에 들어옴");
            isPlayerNear = true;
            interactionPlayer = other.GetComponentInParent<PlayerController>();
            playerUI = other.GetComponentInParent<PlayerInteractUI>();
        }
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exit: " + other.name);

        if (other.CompareTag(playerTag))
        {
            Debug.Log("플레이어가 연구소 범위에서 나감");
            isPlayerNear = false;
            if (playerUI != null) playerUI.HideButton(this);
            interactionPlayer = null;
            playerUI = null;
        }
    }
}