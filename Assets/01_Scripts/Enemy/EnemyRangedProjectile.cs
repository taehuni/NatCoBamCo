using UnityEngine;

// 远程敌人投射物：沿抛物线飞向锁定目标，并在到达终点时结算一次伤害。
// 원거리 적 투사체: 포물선을 따라 고정된 목표로 날아가고 도착 시 피해를 한 번 적용한다.
public class EnemyRangedProjectile : MonoBehaviour
{
    [Header("Flight Data / 비행 데이터")]
    public float moveSpeed = 8f; // 决定飞行总时间，数值越大飞得越快 / 비행 시간을 결정하며 값이 클수록 빠르다
    public float arcHeight = 2f; // 抛物线最高点相对直线轨迹抬高多少 / 직선 궤적보다 포물선 정점이 얼마나 높을지
    public float minimumTravelTime = 0.05f; // 防止距离过近时同一帧瞬间命中 / 거리가 너무 가까울 때 한 프레임에 즉시 명중하는 것을 방지

    private DamageableBuilding targetBuilding;
    private Core targetCore;
    private GameObject targetObject;
    private Vector3 startPosition;
    private Vector3 targetPoint;
    private float damage;
    private float travelDuration;
    private float elapsedTime;
    private bool hasHit;

    void Update()
    {
        // 目标在飞行途中被销毁时，投射物也直接销毁，不再对空目标结算伤害。
        // 비행 중 목표가 파괴되면 투사체도 제거하고 존재하지 않는 목표에 피해를 적용하지 않는다.
        if (targetObject == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsedTime += Time.deltaTime;

        // t 表示当前飞行进度：0 是发射点，1 是目标点。
        // t는 현재 비행 진행도다. 0은 발사 지점, 1은 목표 지점이다.
        float t = Mathf.Clamp01(elapsedTime / travelDuration);

        // 先在发射点和目标点之间做直线插值，再用 4t(1-t) 添加中间高、两端为 0 的弧线高度。
        // 발사점과 목표점 사이를 선형 보간한 뒤 4t(1-t)를 사용해 양 끝은 0이고 중앙은 높은 곡선을 더한다.
        Vector3 straightPosition = Vector3.Lerp(startPosition, targetPoint, t);
        float arcOffset = 4f * arcHeight * t * (1f - t);
        Vector3 nextPosition = straightPosition + Vector3.up * arcOffset;

        // 让投射物朝向这一帧的移动方向，方便以后使用箭、炮弹等有朝向的模型。
        // 화살이나 포탄처럼 방향이 있는 모델을 사용할 수 있도록 이번 프레임의 이동 방향을 바라보게 한다.
        Vector3 moveDirection = nextPosition - transform.position;

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection.normalized);
        }

        transform.position = nextPosition;

        if (t >= 1f)
        {
            HitTarget();
        }
    }

    // 从 EnemyAttack 接收建筑目标与本次攻击伤害。
    // EnemyAttack에서 건물 목표와 이번 공격의 피해량을 전달받는다.
    public void Initialize(DamageableBuilding building, float projectileDamage)
    {
        targetBuilding = building;
        targetCore = null;
        InitializeFlight(building == null ? null : building.gameObject, projectileDamage);
    }

    // Core 不是 DamageableBuilding，所以提供一个 Core 专用初始化入口。
    // Core는 DamageableBuilding이 아니므로 Core 전용 초기화 입구를 제공한다.
    public void Initialize(Core core, float projectileDamage)
    {
        targetBuilding = null;
        targetCore = core;
        InitializeFlight(core == null ? null : core.gameObject, projectileDamage);
    }

    // 记录发射点、命中点和飞行时间，为之后每帧计算抛物线做准备。
    // 발사점, 명중점, 비행 시간을 저장해서 이후 매 프레임 포물선을 계산할 준비를 한다.
    void InitializeFlight(GameObject newTarget, float projectileDamage)
    {
        targetObject = newTarget;
        damage = projectileDamage;
        startPosition = transform.position;
        elapsedTime = 0f;
        hasHit = false;

        if (targetObject == null)
        {
            Destroy(gameObject);
            return;
        }

        // 瞄准目标 Collider 表面离发射点最近的位置；没有 Collider 时会退回目标 Transform 位置。
        // 발사점에서 가장 가까운 목표 Collider 표면을 조준하며 Collider가 없으면 Transform 위치를 사용한다.
        targetPoint = EnemyTargetUtility.GetClosestPointToTarget(startPosition, targetObject);

        float safeMoveSpeed = Mathf.Max(0.01f, moveSpeed);
        float directDistance = Vector3.Distance(startPosition, targetPoint);
        travelDuration = Mathf.Max(
            0.01f,
            Mathf.Max(minimumTravelTime, directDistance / safeMoveSpeed)
        );
    }

    // 命中只允许执行一次，避免同一发投射物重复扣血。
    // 한 투사체가 피해를 중복 적용하지 않도록 명중 처리는 한 번만 실행한다.
    void HitTarget()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        if (targetBuilding != null)
        {
            targetBuilding.GetDamage(damage);
            Debug.Log(gameObject.name + " hit " + targetBuilding.gameObject.name + " for " + damage + " damage");
        }
        else if (targetCore != null)
        {
            targetCore.GetDamage(damage);
            Debug.Log(gameObject.name + " hit " + targetCore.gameObject.name + " for " + damage + " damage");
        }

        Destroy(gameObject);
    }
}
