using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyAttack))]
[RequireComponent(typeof(EnemyTargetSelector))]
[RequireComponent(typeof(EnemyPathBlockHandler))]
[RequireComponent(typeof(BuildingAttackSlotManager))]
public class EnemyBaseAttackBehaviour : MonoBehaviour
{
    [Header("Search Settings / 탐지 설정")]
    public float searchInterval = 0.1f; // 检测间隔 / 탐지 간격
    public float blockedPointSearchRange = 4f; // 堵路点附近寻找建筑的范围 / 막힌 지점 근처에서 건물을 찾는 범위
    public float blockedPathForwardSearchRange = 10f; // 路径被堵时，沿着目标方向继续找阻挡建筑的范围 / 경로가 막혔을 때 목표 방향으로 막는 건물을 더 찾는 범위
    public float buildingMovePointSampleRange = 2f; // 建筑附近寻找可走点的范围 / 건물 근처 이동 가능 지점 검색 범위
    public float navMeshSampleRange = 5f; // 把目标点修正到附近 NavMesh 的范围 / 목표 지점을 근처 NavMesh로 보정하는 범위
    public float targetPathPointExtraDistance = 1f; // 目标外侧额外寻找路径点的距离 / 타겟 바깥쪽 경로 후보 지점을 추가로 찾는 거리
    public float attackPointRefreshInterval = 0.5f; // 重新选择建筑攻击点的间隔 / 건물 공격 위치를 다시 선택하는 간격

    [Header("Attack Settings / 공격 설정")]
    public float buildingTurnSpeed = 360f; // 攻击建筑前转向速度 / 건물 공격 전 회전 속도
    public float attackPointArriveDistance = 0.6f; // 距离攻击点多近才算到达 / 공격 위치에 얼마나 가까워야 도착으로 볼지
    public float buildingAttackStoppingDistance = 0.05f; // 移动到建筑攻击点时使用的停止距离 / 건물 공격 위치로 이동할 때 사용하는 정지 거리
    public float buildingAttackBoxWidth = 1.2f; // 攻击建筑时前方盒子的宽度 / 건물 공격 판정 박스의 너비
    public float buildingAttackBoxHeight = 1.5f; // 攻击建筑时前方盒子的高度 / 건물 공격 판정 박스의 높이
    public float buildingAttackLineRadius = 0.15f; // 攻击建筑时检测阻挡用的线宽 / 건물 공격 시 막힘 확인용 라인 두께
    public bool showBuildingAttackBoxDebug = true; // 显示建筑攻击盒检测日志 / 건물 공격 박스 감지 로그 표시

    [Header("Layer Settings / 레이어 설정")]
    public LayerMask buildingLayer; // 所有玩家建筑层，例如墙和塔 / 모든 플레이어 건물 레이어
    public LayerMask buildingAttackBlockLayer; // 会阻挡建筑攻击的层，例如敌人、环境墙、玩家建筑 / 건물 공격을 막는 레이어
    public LayerMask playerLayer; // 玩家层 / 플레이어 레이어
    public LayerMask wallLayer; // 玩家墙层 / 플레이어 벽 레이어
    public LayerMask towerLayer; // 玩家塔层 / 플레이어 타워 레이어

    private EnemyAI enemyAI;
    private EnemyMovement movement;
    private EnemyAttack attack;
    private EnemyTargetSelector targetSelector;
    private EnemyPathBlockHandler pathBlockHandler;
    private BuildingAttackSlotManager attackSlotManager;

    private Core core;
    private Collider coreCollider;

    private GameObject priorityTarget; // 当前锁定的优先目标 / 현재 고정된 우선 타겟
    private bool isAttackingBlockedWall; // 当前目标是否是因为堵路才锁定的墙 / 현재 타겟이 경로 차단 때문에 고정된 벽인지 여부
    private Vector3 blockedWallDestination; // 这面墙原本挡住的目标点 / 이 벽이 원래 막고 있던 목표 지점
    private GameObject blockedWallOriginalTarget; // 这面墙原本挡住的目标对象 / 이 벽이 원래 막고 있던 목표 오브젝트

    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        movement = GetOrAddComponent<EnemyMovement>();
        attack = GetOrAddComponent<EnemyAttack>();
        targetSelector = GetOrAddComponent<EnemyTargetSelector>();
        pathBlockHandler = GetOrAddComponent<EnemyPathBlockHandler>();
        attackSlotManager = GetOrAddComponent<BuildingAttackSlotManager>();

        movement.Initialize(enemyAI);
        attack.Initialize(enemyAI, movement);
        SetupDefaultBuildingAttackBlockLayer();

        core = FindObjectOfType<Core>();

        if (core != null)
        {
            coreCollider = core.GetComponentInChildren<Collider>();
        }
    }

    void Update()
    {
        if (enemyAI == null || movement == null)
        {
            return;
        }

        if (enemyAI.IsParalyzed())
        {
            return;
        }

        EnemyDefaultLogic();
    }

    void OnDisable()
    {
        if (attackSlotManager != null)
        {
            attackSlotManager.ReleaseAttackPoint();
        }
    }

    void OnDestroy()
    {
        if (attackSlotManager != null)
        {
            attackSlotManager.ReleaseAttackPoint();
        }
    }

    // 敌人在基地场景的主逻辑
    // 기지 장면에서 적의 기본 행동 로직
    void EnemyDefaultLogic()
    {
        UpdatePriorityTarget();

        if (priorityTarget != null)
        {
            MoveOrAttackTarget(priorityTarget);
            return;
        }

        MoveOrAttackCore();
    }

    // 更新当前优先目标：如果目标丢失，就重新找
    // 현재 우선 타겟 갱신: 타겟을 잃으면 다시 찾음
    void UpdatePriorityTarget()
    {
        if (IsTargetInvalid(priorityTarget))
        {
            ClearPriorityTarget();
        }

        if (priorityTarget != null)
        {
            if (isAttackingBlockedWall && IsBlockedWallOriginalPathComplete())
            {
                ClearPriorityTarget();
            }
            else
            {
                float distance = EnemyTargetUtility.GetDistanceToTarget(transform.position, priorityTarget);

                if (distance > targetSelector.loseTargetRange)
                {
                    ClearPriorityTarget();
                }

                return;
            }
        }

        priorityTarget = targetSelector.FindPriorityTarget(
            enemyAI,
            movement,
            attackSlotManager,
            playerLayer,
            wallLayer,
            towerLayer,
            targetSelector.detectRange,
            buildingMovePointSampleRange
        );

        isAttackingBlockedWall = false;
    }

    // 判断当前锁定目标是否已经失效
    // 현재 고정된 타겟이 더 이상 유효하지 않은지 확인
    bool IsTargetInvalid(GameObject target)
    {
        if (target == null)
        {
            return true;
        }

        if (!target.activeInHierarchy)
        {
            return true;
        }

        DamageableBuilding building = target.GetComponentInParent<DamageableBuilding>();

        if (building != null && building.hp <= 0f)
        {
            return true;
        }

        return false;
    }

    // 清空当前锁定目标
    // 현재 고정된 타겟을 비움
    void ClearPriorityTarget()
    {
        priorityTarget = null;
        isAttackingBlockedWall = false;
        blockedWallOriginalTarget = null;

        if (attackSlotManager != null)
        {
            attackSlotManager.ReleaseAttackPoint();
        }
    }

    // 锁定堵路墙，让敌人持续攻击这面墙，而不是每帧重新找 Core
    // 경로를 막은 벽을 고정해서 매 프레임 Core를 다시 찾지 않게 함
    void LockBlockedWall(DamageableBuilding wall, Vector3 destination, GameObject originalTarget)
    {
        if (wall == null)
        {
            return;
        }

        priorityTarget = wall.gameObject;
        isAttackingBlockedWall = true;
        blockedWallDestination = destination;
        blockedWallOriginalTarget = originalTarget;
    }

    // 检查堵路墙原本挡住的目标是否已经重新可达
    // 경로 차단 벽이 원래 막고 있던 목표가 다시 도달 가능한지 확인
    bool IsBlockedWallOriginalPathComplete()
    {
        if (blockedWallOriginalTarget != null)
        {
            EnemyPathBlockHandler.PathChoice pathChoice;

            if (!pathBlockHandler.TryFindBestPathToTarget(
                blockedWallOriginalTarget,
                enemyAI,
                movement,
                navMeshSampleRange,
                targetPathPointExtraDistance,
                out pathChoice))
            {
                return false;
            }

            return pathChoice.isComplete;
        }

        EnemyPathBlockHandler.PathChoice destinationPath;
        return pathBlockHandler.IsPathComplete(blockedWallDestination, movement, navMeshSampleRange, out destinationPath);
    }

    // 移动到优先目标，距离够近就攻击
    // 우선 타겟으로 이동하고, 충분히 가까우면 공격
    void MoveOrAttackTarget(GameObject target)
    {
        if (target == null)
        {
            ClearPriorityTarget();
            return;
        }

        DamageableBuilding targetBuilding = target.GetComponentInParent<DamageableBuilding>();

        if (targetBuilding != null)
        {
            MoveOrAttackBuilding(targetBuilding);
            return;
        }

        attackSlotManager.ReleaseAttackPoint();

        EnemyPathBlockHandler.PathChoice targetPath;

        if (!pathBlockHandler.TryFindBestPathToTarget(
            target,
            enemyAI,
            movement,
            navMeshSampleRange,
            targetPathPointExtraDistance,
            out targetPath))
        {
            return;
        }

        if (HandleBlockedPath(targetPath, target))
        {
            return;
        }

        float distance = EnemyTargetUtility.GetDistanceToTarget(transform.position, target);

        if (distance <= attack.attackRange)
        {
            // 玩家受伤逻辑以后再加
            // 플레이어 피해 로직은 나중에 추가
            movement.Stop();
            return;
        }

        movement.MoveToPosition(targetPath.destination, navMeshSampleRange);
    }

    // 移动到核心，距离够近就攻击核心
    // 코어로 이동하고, 충분히 가까우면 코어를 공격
    void MoveOrAttackCore()
    {
        if (core == null)
        {
            return;
        }

        attackSlotManager.ReleaseAttackPoint();

        EnemyPathBlockHandler.PathChoice corePath;

        if (!pathBlockHandler.TryFindBestPathToTarget(
            core.gameObject,
            enemyAI,
            movement,
            navMeshSampleRange,
            targetPathPointExtraDistance,
            out corePath))
        {
            return;
        }

        if (HandleBlockedPath(corePath, core.gameObject))
        {
            return;
        }

        float distance = GetDistanceToCore();

        if (distance <= attack.attackRange)
        {
            attack.AttackCore(core);
            return;
        }

        movement.MoveToPosition(corePath.destination, navMeshSampleRange);
    }

    // 检查路径是否被堵住；如果被堵住，就尝试找堵路建筑并攻击
    // 경로가 막혔는지 확인하고, 막혔다면 막고 있는 건물을 찾아 공격
    bool HandleBlockedPath(EnemyPathBlockHandler.PathChoice pathChoice, GameObject originalTarget)
    {
        if (!pathChoice.hasPath)
        {
            return false;
        }

        if (pathChoice.isComplete)
        {
            return false;
        }

        DamageableBuilding blockingBuilding = pathBlockHandler.FindBuildingBlockingPath(
            pathChoice.lastReachablePoint,
            pathChoice.destination, 
            buildingLayer,
            blockedPointSearchRange,
            blockedPathForwardSearchRange
        );

        if (blockingBuilding != null)
        {
            LockBlockedWall(blockingBuilding, pathChoice.destination, originalTarget);
            MoveOrAttackBuilding(blockingBuilding);
            return true;
        }

        if (Vector3.Distance(transform.position, pathChoice.lastReachablePoint) < 0.5f)
        {
            return false;
        }

        movement.MoveToPosition(pathChoice.lastReachablePoint, navMeshSampleRange);
        return true;
    }

    // 移动到建筑附近；距离够近就攻击建筑
    // 건물 근처로 이동하고, 충분히 가까우면 건물을 공격
    void MoveOrAttackBuilding(DamageableBuilding building)
    {
        if (building == null || building.hp <= 0f)
        {
            ClearPriorityTarget();
            return;
        }

        Vector3 movePoint;
        float preciseAttackPointArriveDistance = GetPreciseAttackPointArriveDistance();

        bool hasAttackPoint = attackSlotManager.TryReserveAttackPoint(
            building,
            enemyAI,
            movement,
            attackPointRefreshInterval,
            buildingMovePointSampleRange,
            preciseAttackPointArriveDistance,
            out movePoint
        );

        if (hasAttackPoint)
        {
            bool isNearAttackPoint = attackSlotManager.IsNearAttackPoint(
                movement,
                movePoint,
                preciseAttackPointArriveDistance
            );

            float distanceToBuilding = EnemyTargetUtility.GetDistanceToTarget(transform.position, building.gameObject);
            bool isCloseEnoughToTryAttack = distanceToBuilding <= GetBuildingAttackReach() + 0.05f;

            if (!isNearAttackPoint && !isCloseEnoughToTryAttack)
            {
                movement.MoveToPosition(movePoint, navMeshSampleRange, buildingAttackStoppingDistance);
                return;
            }

            movement.Stop();
            movement.SetAutoRotation(false);
            FaceTarget(building.gameObject);

            if (CanHitBuildingWithFrontBox(building))
            {
                attack.AttackBuilding(building);
                return;
            }

            if (!isNearAttackPoint)
            {
                movement.MoveToPosition(movePoint, navMeshSampleRange, buildingAttackStoppingDistance);
            }

            return;
        }

        movement.SetAutoRotation(false);
        FaceTarget(building.gameObject);

        if (CanHitBuildingWithFrontBox(building))
        {
            movement.Stop();
            attack.AttackBuilding(building);
            return;
        }

        movePoint = attackSlotManager.GetMovePointNearTarget(building.gameObject, movement, buildingMovePointSampleRange);
        movement.MoveToPosition(movePoint, navMeshSampleRange, buildingAttackStoppingDistance);
    }

    // 建筑攻击点需要更精确，不能直接用普通 NavMeshAgent 停止距离
    // 건물 공격 위치는 더 정확해야 하므로 일반 NavMeshAgent 정지 거리만 사용하지 않음
    float GetPreciseAttackPointArriveDistance()
    {
        float agentRadius = movement == null ? 0f : movement.Radius;
        float minimumAllowedDistance = agentRadius + buildingAttackStoppingDistance + 0.1f;

        return Mathf.Max(attackPointArriveDistance, minimumAllowedDistance);
    }

    float GetBuildingAttackReach()
    {
        float agentRadius = movement == null ? 0f : movement.Radius;
        return attack.attackRange + agentRadius;
    }

    // 判断建筑是否已经进入敌人前方攻击盒子
    // 건물이 적 전방 공격 판정 박스 안에 들어왔는지 확인
    bool CanHitBuildingWithFrontBox(DamageableBuilding building)
    {
        if (attackSlotManager == null)
        {
            return false;
        }

        bool isInsideAttackBox = attackSlotManager.IsBuildingInsideFrontAttackBox(
            building,
            enemyAI,
            buildingLayer,
            buildingAttackBoxWidth,
            buildingAttackBoxHeight,
            showBuildingAttackBoxDebug
        );

        if (!isInsideAttackBox)
        {
            return false;
        }

        return attackSlotManager.HasClearAttackLineToBuilding(
            building,
            enemyAI,
            buildingAttackBlockLayer,
            buildingAttackLineRadius,
            showBuildingAttackBoxDebug
        );
    }

    // 攻击前让敌人转向建筑
    // 공격 전에 적이 건물을 바라보게 회전
    void FaceTarget(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, target);
        movement.FacePointInstant(closestPoint);
    }

    void SetupDefaultBuildingAttackBlockLayer()
    {
        if (buildingAttackBlockLayer.value != 0)
        {
            return;
        }

        buildingAttackBlockLayer = LayerMask.GetMask(
            "Default",
            "Enemy",
            "Wall",
            "PlayerWall",
            "PlayerTower"
        );
    }

    // 敌人到 Core 表面的距离
    // 적에서 Core 표면까지의 거리
    float GetDistanceToCore()
    {
        if (core == null)
        {
            return Mathf.Infinity;
        }

        if (coreCollider != null)
        {
            Vector3 closestPoint = coreCollider.ClosestPoint(transform.position);
            return Vector3.Distance(transform.position, closestPoint);
        }

        return Vector3.Distance(transform.position, core.transform.position);
    }

    // 选中敌人时显示当前预约的建筑攻击点
    // 적을 선택했을 때 현재 예약한 건물 공격 위치를 표시
    void OnDrawGizmosSelected()
    {
        BuildingAttackSlotManager slotManager = attackSlotManager;

        if (slotManager == null)
        {
            slotManager = GetComponent<BuildingAttackSlotManager>();
        }

        if (slotManager == null)
        {
            return;
        }

        slotManager.DrawCurrentAttackPointGizmos(GetPreciseAttackPointArriveDistance());

        EnemyAI gizmoEnemyAI = enemyAI;

        if (gizmoEnemyAI == null)
        {
            gizmoEnemyAI = GetComponent<EnemyAI>();
        }

        slotManager.DrawFrontAttackBoxGizmos(
            gizmoEnemyAI,
            buildingAttackBoxWidth,
            buildingAttackBoxHeight
        );
    }

    T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();

        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}
