using UnityEngine;
using UnityEngine.UI;

public class MedicalCenter : MonoBehaviour, IInteractionTarget
{
    [Header("상호작용")]
    public float detectRange = 4f;
    public LayerMask playerLayer;
    public float interactTime = 2f;

    [Header("회복")]
    public int healAmount = 30;

    private bool playerInRange;
    private bool isHealing;
    private float healTimer;

    private PlayerInteractUI playerUI;
    private Slider playerSlider;
    private PlayerController player;

    private PlayerController interactionPlayer;
    public KeyCode InteractionKey => KeyCode.E;
    public bool CanBeginInteraction => !isHealing;
    public bool InteractionInProgress => isHealing;
    public bool IsPlayerInInteractionRange(PlayerController player) =>
        InteractionSelection.IsInRange(this, player, detectRange, playerLayer);

    void OnEnable() => InteractionSelection.Register(this);

    void OnDisable()
    {
        StopHeal();
        if (playerUI != null) playerUI.HideButton(this);
        InteractionSelection.Unregister(this);
        interactionPlayer = null;
    }

    void Update()
    {
        CheckPlayerNear();

        if (playerInRange && Input.GetKeyDown(KeyCode.E) && InteractionSelection.TryBegin(this, interactionPlayer))
        {
            StartHeal();
        }

        if (isHealing)
        {
            UpdateHeal();
        }
    }

    void CheckPlayerNear()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        playerInRange = players.Length > 0;
        interactionPlayer = playerInRange ? players[0].GetComponentInParent<PlayerController>() : null;

        if (playerInRange)
        {
            player = players[0].GetComponentInParent<PlayerController>();

            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();

            if (playerUI != null)
            {
                playerSlider = playerUI.interactSlider;
                playerUI.ShowButton("회복하기(E)", this);
            }
        }
        else
        {
            if (playerUI != null)
            {
                playerUI.HideButton(this);
            }

            StopHeal();
            playerUI = null;
            player = null;
        }
    }

    void StartHeal()
    {
        isHealing = true;
        healTimer = 0f;

        if (playerUI != null)
            playerUI.ShowSlider(this);

        if (playerSlider != null)
            playerUI?.SetProgress(0f, this);
    }

    void UpdateHeal()
    {
        healTimer += Time.deltaTime;

        if (playerSlider != null)
            playerUI?.SetProgress(healTimer / interactTime, this);

        if (healTimer >= interactTime)
        {
            HealComplete();
        }
    }

    void HealComplete()
    {
        isHealing = false;

        if (player != null)
        {
            player.Heal(healAmount);
            Debug.Log($"회복 완료 : {healAmount}");
        }

        if (playerSlider != null)
            playerUI?.SetProgress(0f, this);

        if (playerUI != null)
            playerUI.HideSlider(this);
    }

    void StopHeal()
    {
        isHealing = false;
        healTimer = 0f;

        if (playerSlider != null)
            playerUI?.SetProgress(0f, this);

        if (playerUI != null)
            playerUI.HideSlider(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
