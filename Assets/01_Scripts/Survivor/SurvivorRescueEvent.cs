using System.Collections.Generic;
using UnityEngine;

// 탐색 중 생존자 구조 이벤트.
// ResourceNode.cs / MedicalCenter.cs 와 동일한 패턴(OverlapSphere + PlayerInteractUI + E키)을 따른다.
// 태훈 수정: 새 프리팹을 스폰하던 방식에서, 이 오브젝트에 이미 붙어있는 SurvivorAI 를 그 자리에서 구출 처리하는 방식으로 변경.
// Survivor_Mechanic/Gatherer/Researcher 프리팹처럼 SurvivorAI 가 이미 붙어있는 오브젝트에 이 컴포넌트를 같이 붙여서 사용.
// 흐름: 플레이어 접근 -> 주변 몬스터(enemyLayer) 존재 확인 -> 없으면 E키로 구조
//      -> SurvivorAI.state 를 Rescued 로 변경 -> SurvivorManager 에 합류
public class SurvivorRescueEvent : MonoBehaviour
{
    [Header("상호작용")]
    public float detectRange = 4f;
    public LayerMask playerLayer;

    [Header("생존자 정보")]
    [Tooltip("비어있으면 SurvivorAI 에 이미 설정된 이름을 그대로 사용")]
    public string survivorName;

    [Header("경비 몬스터 체크")]
    public float guardCheckRange = 6f;
    public LayerMask enemyLayer;

    private bool playerInRange;
    private bool rescued;
    private PlayerInteractUI playerUI;

    // 태훈 추가: 씬을 다시 로드해도(파밍씬 재입장) 이미 구출한 생존자가 트랩 상태로 재등장(=중복)하지 않도록 static으로 기억
    private static readonly HashSet<string> rescuedIds = new HashSet<string>();
    private string RescueId => $"{gameObject.scene.name}:{gameObject.name}:{transform.position}";

    void Start()
    {
        if (rescuedIds.Contains(RescueId))
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        CheckPlayerNear();

        if (playerInRange && !rescued && GuardsCleared() && Input.GetKeyDown(KeyCode.E))
        {
            Rescue();
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
                if (GuardsCleared())
                {
                    playerUI.ShowButton("생존자 구조(E)");
                }
                else
                {
                    playerUI.ShowButton("주변 몬스터를 먼저 처치하세요");
                }
            }
        }
        else
        {
            if (playerUI != null)
            {
                playerUI.HideButton();
            }

            playerUI = null;
        }
    }

    bool GuardsCleared()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, guardCheckRange, enemyLayer);
        return enemies.Length == 0;
    }

    void Rescue()
    {
        rescued = true;

        // 태훈 수정: SurvivorManager.AddSurvivor() 가 이 오브젝트를 자기 밑(DontDestroyOnLoad)으로 재부모화하기 전에
        // RescueId를 먼저 계산해둠 (재부모화 후에 계산하면 gameObject.scene 이 바뀌어서 씬 재입장 시 비교가 안 맞음)
        string rescueId = RescueId;

        SurvivorAI survivorAI = GetComponent<SurvivorAI>();

        if (survivorAI == null)
        {
            Debug.LogError("이 오브젝트에 SurvivorAI 컴포넌트가 없습니다");
            return;
        }

        if (!string.IsNullOrEmpty(survivorName))
        {
            survivorAI.survivorName = survivorName;
        }

        survivorAI.state = SurvivorAI.SurvivorState.Rescued;

        if (SurvivorManager.Instance != null)
        {
            SurvivorManager.Instance.AddSurvivor(survivorAI);
        }

        Debug.Log($"{survivorAI.survivorName} 구조 완료");

        if (playerUI != null)
        {
            playerUI.HideButton();
        }

        // TODO: 아트 팀 - 구조 연출(파티클/사운드)
        rescuedIds.Add(rescueId);

        // 태훈 수정: 구출 즉시 파밍씬에서 사라지게 함. SurvivorManager가 DontDestroyOnLoad로 데리고 있다가
        // 다음 씬(쉘터 등)이 로드되면 그 씬의 거주구역/연구실 기준으로 다시 활성화해서 배치함
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, guardCheckRange);
    }
}
