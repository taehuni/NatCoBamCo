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
    [Tooltip("실제 게임에서는 GameManager의 밤 상태를 사용한다. GameManager가 없는 테스트 씬에서는 SetNight로 설정한다.")]
    public bool isNight;

    private SurvivorAI survivorAI;
    private SurvivorMovement movement;
    private DamageableBuilding currentTarget;
    private bool controlsMovement;

    void Awake()
    {
        survivorAI = GetComponent<SurvivorAI>();
        movement = GetComponent<SurvivorMovement>();
    }

    void Update()
    {
        var game = GameManager.Instance;
        if (game != null)
            SetNight(game.currentPhase == GameManager.GamePhase.NightStart ||
                game.currentPhase == GameManager.GamePhase.Defense);

        if (!isNight || survivorAI == null || !survivorAI.IsAvailable ||
            survivorAI.role != SurvivorAI.SurvivorRole.Mechanic ||
            survivorAI.state != SurvivorAI.SurvivorState.Rescued ||
            (game != null && (game.IsGameOver || game.IsRestarting)))
        {
            ReleaseMovement();
            return;
        }

        if (Time.timeScale <= 0f || !movement.IsReady) return;

        if (!controlsMovement)
        {
            movement.Stop(); // 낮의 배회 목적지로 계속 걷지 않도록 이동을 넘겨받는다.
            controlsMovement = true;
        }

        if (!CanRepair(currentTarget))
        {
            movement.Stop();
            currentTarget = FindMostDamagedTower();
        }

        if (currentTarget == null)
        {
            return;
        }

        float distance = EnemyTargetUtility.GetDistanceToTarget(transform.position, currentTarget.gameObject);

        if (distance <= repairRange)
        {
            movement.Stop();
            currentTarget.Repair(survivorAI.repairPower * Time.deltaTime);
        }
        else
        {
            Vector3 repairPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, currentTarget.gameObject);
            movement.MoveToPosition(repairPoint);
        }
    }

    bool CanRepair(DamageableBuilding building) => building != null &&
        building.isActiveAndEnabled && building.Health.NeedsRepair &&
        (towerLayer.value & (1 << building.gameObject.layer)) != 0;

    DamageableBuilding FindMostDamagedTower()
    {
        Collider[] towers = Physics.OverlapSphere(transform.position, searchRange, towerLayer);

        DamageableBuilding best = null;
        float bestMissingHp = 0f;

        for (int i = 0; i < towers.Length; i++)
        {
            DamageableBuilding building = towers[i].GetComponentInParent<DamageableBuilding>();

            if (!CanRepair(building))
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
        if (!night) ReleaseMovement();
    }

    void ReleaseMovement()
    {
        currentTarget = null;
        if (controlsMovement && movement != null) movement.Stop();
        controlsMovement = false;
    }

    void OnDisable() => ReleaseMovement();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, searchRange);
    }
}
