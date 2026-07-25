using UnityEngine;

// 탐색 중 생존자 구조 이벤트.
// ResourceNode.cs / MedicalCenter.cs 와 동일한 패턴(OverlapSphere + PlayerInteractUI + E키)을 따른다.
// 흐름: 플레이어 접근 -> 주변 몬스터(enemyLayer) 존재 확인 -> 없으면 E키로 구조
//      -> survivorPrefab(SurvivorAI 포함) 을 Instantiate -> SurvivorManager 에 합류
public class SurvivorRescueEvent : MonoBehaviour
{
    [Header("상호작용")]
    public float detectRange = 4f;
    public LayerMask playerLayer;

    [Header("생존자 정보")]
    [Tooltip("SurvivorAI + SurvivorMovement (+ 정비공이면 SurvivorMechanicBehaviour) 가 붙어있는 프리팹")]
    public GameObject survivorPrefab;
    public string survivorName = "이름 없는 생존자";
    public SurvivorAI.SurvivorRole survivorRole;
    public Transform spawnPoint; // 비어있으면 이 오브젝트 위치를 사용

    [Header("경비 몬스터 체크")]
    public float guardCheckRange = 6f;
    public LayerMask enemyLayer;

    private bool playerInRange;
    private bool rescued;
    private PlayerInteractUI playerUI;

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

        if (survivorPrefab == null)
        {
            Debug.LogError("survivorPrefab 이 지정되지 않았습니다");
            return;
        }

        Vector3 spawnAt = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject instance = Instantiate(survivorPrefab, spawnAt, Quaternion.identity);

        SurvivorAI survivorAI = instance.GetComponent<SurvivorAI>();

        if (survivorAI != null)
        {
            survivorAI.survivorName = survivorName;
            survivorAI.role = survivorRole;

            if (SurvivorManager.Instance != null)
            {
                SurvivorManager.Instance.AddSurvivor(survivorAI);
            }
        }

        Debug.Log($"{survivorName} 구조 완료");

        if (playerUI != null)
        {
            playerUI.HideButton();
        }

        // TODO: 아트 팀 - 구조 연출(파티클/사운드)
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
