using UnityEngine;
using UnityEngine.UI;

public class MedicalCenter : MonoBehaviour
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

    void Update()
    {
        CheckPlayerNear();

        if (playerInRange && Input.GetKeyDown(KeyCode.E) && !isHealing)
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

        if (playerInRange)
        {
            player = players[0].GetComponentInParent<PlayerController>();

            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();

            if (playerUI != null)
            {
                playerSlider = playerUI.interactSlider;
                playerUI.ShowButton("회복하기(E)");
            }
        }
        else
        {
            if (playerUI != null)
            {
                playerUI.HideButton();
            }

            playerUI = null;
            player = null;

            StopHeal();
        }
    }

    void StartHeal()
    {
        isHealing = true;
        healTimer = 0f;

        if (playerUI != null)
            playerUI.ShowSlider();

        if (playerSlider != null)
            playerSlider.value = 0f;
    }

    void UpdateHeal()
    {
        healTimer += Time.deltaTime;

        if (playerSlider != null)
            playerSlider.value = healTimer / interactTime;

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
            playerSlider.value = 0f;

        if (playerUI != null)
            playerUI.HideSlider();
    }

    void StopHeal()
    {
        isHealing = false;
        healTimer = 0f;

        if (playerSlider != null)
            playerSlider.value = 0f;

        if (playerUI != null)
            playerUI.HideSlider();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
