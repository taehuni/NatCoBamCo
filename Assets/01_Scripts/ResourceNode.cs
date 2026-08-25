using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceNode : MonoBehaviour
{
    public float detectRange = 2f;
    public LayerMask playerLayer;

    public float collectTime = 3f;

    // 태훈 추가: 획득 자원 종류/수량
    public ResourceType resourceType;
    public int amount = 10;

    private bool playerInRange;
    private bool isCollecting;
    private float collectTimer;
    private PlayerInteractUI playerUI;
    private Slider playerSlider;

    // 태훈 추가: 씬을 다시 로드해도(파밍씬 재입장) 이미 채집한 노드가 초기화되어 재등장하지 않도록 static으로 기억
    private static readonly HashSet<string> collectedNodeIds = new HashSet<string>();
    private string NodeId => $"{gameObject.scene.name}:{gameObject.name}:{transform.position}";

    void Awake()
    {
        if (collectedNodeIds.Contains(NodeId))
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        CheckPlayerNear();

        if (playerInRange && Input.GetKeyDown(KeyCode.E))
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


        if (playerInRange)
        {
            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();
            if (playerUI != null)
            {
                playerSlider = playerUI.interactSlider;
                playerUI.ShowButton("채집하기(E)");
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

        if (!playerInRange)
        {
            StopCollect();
        }
    }

    void StartCollect()
    {
        isCollecting = true;
        collectTimer = 0f;

        if (playerUI != null)
        {
            playerUI.ShowSlider();
        }

        if (playerSlider != null)
        {
            playerSlider.value = 0f;
        }
    }

    void UpdateCollect()
    {
        collectTimer += Time.deltaTime;

        if (playerSlider != null)
        {
            playerSlider.value = collectTimer / collectTime;
        }

        if (collectTimer >= collectTime)
        {
            CollectComplete();
        }
    }

    void CollectComplete()
    {
        isCollecting = false;
        collectTimer = 0f;

        if (playerSlider != null)
        {
            playerSlider.value = 0f;
        }

        if (playerUI != null)
        {
            playerUI.HideSlider();
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
            playerUI.HideButton();
        }

        collectedNodeIds.Add(NodeId);
        gameObject.SetActive(false);
    }

    void StopCollect()
    {
        isCollecting = false;
        collectTimer = 0f;

        if (playerSlider != null)
        {
            playerSlider.value = 0f;
        }

        if (playerUI != null)
        {
            playerUI.HideSlider();
        }

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}