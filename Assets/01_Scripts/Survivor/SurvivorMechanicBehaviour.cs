using UnityEngine;

// 정비공(Mechanic) 전용 행동. 밤에 가장 많이 손상된 타워를 찾아 이동한 뒤 도착하면 수리한다.
// SurvivorAI.role 이 Mechanic 인 생존자 프리팹에 이 스크립트도 같이 붙여야 함.
[RequireComponent(typeof(SurvivorAI))]
[RequireComponent(typeof(SurvivorMovement))]
public class SurvivorMechanicBehaviour : MonoBehaviour
{
    [Header("타워 탐색")]
    public LayerMask towerLayer;
    public float searchRange = 30f;
    public float repairRange = 2f;

    [Header("밤 여부")]
    [Tooltip("TODO: 낮/밤 시스템 담당자가 밤이 시작될 때 SetNight(true), 낮이 시작될 때 SetNight(false) 를 호출해줘야 함. 지금은 임시로 인스펙터에서 체크 가능.")]
    public bool isNight;

    private SurvivorAI survivorAI;
    private SurvivorMovement movement;
    private DamageableBuilding currentTarget;

    void Awake()
    {
        survivorAI = GetComponent<SurvivorAI>();
        movement = GetComponent<SurvivorMovement>();
    }

    void Update()
    {
        if (!isNight || survivorAI == null || !survivorAI.IsAvailable)
        {
            return;
        }

        // 태훈 추가: 구출되기 전에는 타워 수리하러 가지 않음
        if (survivorAI.state != SurvivorAI.SurvivorState.Rescued)
        {
            return;
        }

        if (currentTarget == null || currentTarget.hp >= currentTarget.maxHp)
        {
            currentTarget = FindMostDamagedTower();
        }

        if (currentTarget == null)
        {
            return;
        }

        Vector3 repairPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, currentTarget.gameObject);
        movement.MoveToPosition(repairPoint);

        float distance = EnemyTargetUtility.GetDistanceToTarget(transform.position, currentTarget.gameObject);

        if (distance <= repairRange)
        {
            currentTarget.Repair(survivorAI.repairPower * Time.deltaTime);
        }
    }

    DamageableBuilding FindMostDamagedTower()
    {
        Collider[] towers = Physics.OverlapSphere(transform.position, searchRange, towerLayer);

        DamageableBuilding best = null;
        float bestMissingHp = 0f;

        for (int i = 0; i < towers.Length; i++)
        {
            DamageableBuilding building = towers[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            float missing = building.maxHp - building.hp;

            if (missing > bestMissingHp)
            {
                bestMissingHp = missing;
                best = building;
            }
        }

        return best;
    }

    // 낮/밤 시스템에서 호출
    public void SetNight(bool night)
    {
        isNight = night;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, searchRange);
    }
}
