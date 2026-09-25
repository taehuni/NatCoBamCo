using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceNode : MonoBehaviour, IInteractionTarget
{
    public float detectRange = 2f;
    public LayerMask playerLayer;

    public float collectTime = 3f;

    // 태훈 추가: 획득 자원 종류/수량
    public ResourceType resourceType;
    public int amount = 10;

    [SerializeField] private DailyResourceSpawner dailySpawner;

    private bool playerInRange;
    private bool isCollecting;
    private float collectTimer;
    private PlayerInteractUI playerUI;
    private Slider playerSlider;

    // 태훈 추가: 씬을 다시 로드해도(파밍씬 재입장) 이미 채집한 노드가 초기화되어 재등장하지 않도록 static으로 기억
    private static readonly HashSet<string> collectedNodeIds = new HashSet<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetSession() => collectedNodeIds.Clear();
    private string NodeId => $"{gameObject.scene.name}:{gameObject.name}:{transform.position}";

    private PlayerController interactionPlayer;
    public KeyCode InteractionKey => KeyCode.E;
    public bool CanBeginInteraction => !isCollecting;
    public bool InteractionInProgress => isCollecting;
    public bool IsPlayerInInteractionRange(PlayerController player) =>
        InteractionSelection.IsInRange(this, player, detectRange, playerLayer);

    void OnEnable() => InteractionSelection.Register(this);

    void OnDisable()
    {
        StopCollect();
        if (playerUI != null) playerUI.HideButton(this);
        InteractionSelection.Unregister(this);
        interactionPlayer = null;
    }

    void Awake()
    {
        if (dailySpawner == null && collectedNodeIds.Contains(NodeId))
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        CheckPlayerNear();

        if (playerInRange && Input.GetKeyDown(KeyCode.E) && InteractionSelection.TryBegin(this, interactionPlayer))
        {
            StartCollect();
        }

        if (isCollecting)
        {
            UpdateCollect();
        }
    }

    void CheckPlayerNear()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        playerInRange = players.Length > 0;
        interactionPlayer = playerInRange ? players[0].GetComponentInParent<PlayerController>() : null;


        if (playerInRange)
        {
            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();
            if (playerUI != null)
            {
                playerSlider = playerUI.interactSlider;
                playerUI.ShowButton("채집하기(E)", this);
            }
        }
        else
        {
            StopCollect();
            if (playerUI != null)
            {
                playerUI.HideButton(this);
                playerUI = null;
            }
        }
    }

    void StartCollect()
    {
        isCollecting = true;
        collectTimer = 0f;

        if (playerUI != null)
        {
            playerUI.ShowSlider(this);
        }

        if (playerSlider != null)
        {
            playerUI?.SetProgress(0f, this);
        }
    }

    void UpdateCollect()
    {
        collectTimer += Time.deltaTime;

        if (playerSlider != null)
        {
            playerUI?.SetProgress(collectTimer / collectTime, this);
        }

        if (collectTimer >= collectTime)
        {
            CollectComplete();
        }
    }

    void CollectComplete()
    {
        // A day rollover cancels the old action before it can award the new day's resource.
        if (dailySpawner != null && !dailySpawner.CanCollect(this)) return;

        isCollecting = false;
        collectTimer = 0f;

        if (playerSlider != null)
        {
            playerUI?.SetProgress(0f, this);
        }

        if (playerUI != null)
        {
            playerUI.HideSlider(this);
        }

        // Keep the node available if the scene has not connected its inventory.
        if (ResourceInventory.Instance == null)
        {
            Debug.LogWarning("Resource inventory is missing; resource node was not consumed.", this);
            return;
        }

        // 태훈 수정: 로그만 찍던 것을 실제 인벤토리 지급으로 변경 + 채집가 보너스(SurvivorManager.GetGatherBonus) 적용
        if (ResourceInventory.Instance != null)
        {
            float gatherBonus = SurvivorManager.Instance != null ? SurvivorManager.Instance.GetGatherBonus() : 0f;
            int finalAmount = Mathf.RoundToInt(amount * (1f + gatherBonus));

            ResourceInventory.Instance.Add(resourceType, finalAmount);

            Debug.Log($"Collect Complete: {resourceType} +{finalAmount} (기본 {amount}, 채집가 보너스 {gatherBonus:P0})");
        }

        // 태훈 추가: 1회성 채집 - 완료되면 노드 비활성화 (재채집 불가)
        if (playerUI != null)
        {
            playerUI.HideButton(this);
        }

        if (dailySpawner != null)
        {
            dailySpawner.MarkCollected(this);
        }
        else
        {
            collectedNodeIds.Add(NodeId);
            gameObject.SetActive(false);
        }
    }

    void StopCollect()
    {
        isCollecting = false;
        collectTimer = 0f;

        if (playerSlider != null)
        {
            playerUI?.SetProgress(0f, this);
        }

        if (playerUI != null)
        {
            playerUI.HideSlider(this);
        }

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
