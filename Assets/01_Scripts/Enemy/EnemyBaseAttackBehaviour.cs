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
    public float priorityTargetSwitchPathAdvantage = 1f; // 新目标路线至少短多少米才允许切换 / 새 타깃 경로가 최소 몇 미터 짧아야 교체할지

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
    private float nextPriorityTargetSearchTime; // 下一次允许重新比较优先目标的时间 / 다음 우선 타깃 재비교 가능 시간

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

    // 更新当前优先目标：处理目标失效、丢失和 Tank 的定时重新比较。
    // 현재 우선 타깃 갱신: 타깃 무효, 이탈, Tank의 주기적인 재비교를 처리한다.
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
                else
                {
                    TryRefreshTankPriorityTarget();
                    return;
                }
            }
        }

        GameObject newTarget = FindPriorityTarget();

        SetPriorityTarget(newTarget);
        nextPriorityTargetSearchTime = Time.time + Mathf.Max(0.05f, searchInterval);
    }

    // 按敌人种类查找当前感知范围内的优先目标。
    // 적 종류에 따라 현재 감지 범위 안의 우선 타깃을 찾는다.
    GameObject FindPriorityTarget()
    {
        return targetSelector.FindPriorityTarget(
            enemyAI,
            movement,
            attackSlotManager,
            playerLayer,
            wallLayer,
            towerLayer,
            targetSelector.detectRange,
            buildingMovePointSampleRange
        );
    }

    // Tank 会定时重新观察附近墙体，但只有新墙的可达路径明显更短时才更换目标。
    // Tank는 주기적으로 주변 벽을 다시 확인하지만 새 벽의 도달 경로가 확실히 더 짧을 때만 타깃을 바꾼다.
    void TryRefreshTankPriorityTarget()
    {
        if (Time.time < nextPriorityTargetSearchTime)
        {
            return;
        }

        nextPriorityTargetSearchTime = Time.time + Mathf.Max(0.05f, searchInterval);

        if (enemyAI.ClassTraits == null || enemyAI.ClassTraits.enemyClass != EnemyAI.EnemyClass.Tank)
        {
            return;
        }

        // 经过堵路验证锁定的墙必须先处理，不让普通感知搜索把它随便替换掉。
        // 실제 경로 차단 검증으로 고정한 벽은 먼저 처리해야 하므로 일반 감지 검색으로 교체하지 않는다.
        if (isAttackingBlockedWall)
        {
            return;
        }

        DamageableBuilding currentBuilding = priorityTarget.GetComponentInParent<DamageableBuilding>();

        if (currentBuilding == null)
        {
            return;
        }

        // 已经靠近当前墙并准备攻击时保持目标，避免攻击过程中突然转身。
        // 현재 벽 가까이에서 공격을 준비 중이면 공격 도중 갑자기 방향을 바꾸지 않도록 타깃을 유지한다.
        float distanceToCurrentBuilding = EnemyTargetUtility.GetDistanceToTarget(transform.position, currentBuilding.gameObject);

        if (distanceToCurrentBuilding <= GetBuildingAttackReach() + 0.05f)
        {
            return;
        }

        GameObject newTarget = FindPriorityTarget();

        if (newTarget == null || newTarget == priorityTarget)
        {
            return;
        }

        DamageableBuilding newBuilding = newTarget.GetComponentInParent<DamageableBuilding>();

        if (newBuilding == null)
        {
            return;
        }

        float newPathLength;

        // 新墙必须有一条可以完整走到攻击位置的路径，否则不切换。
        // 새 벽의 공격 위치까지 완전한 경로가 있어야 하며, 그렇지 않으면 교체하지 않는다.
        if (!targetSelector.TryGetReachableBuildingPathLength(
            newBuilding.gameObject,
            enemyAI,
            movement,
            attackSlotManager,
            buildingMovePointSampleRange,
            out newPathLength))
        {
            return;
        }

        float currentPathLength;
        bool canReachCurrentBuilding = targetSelector.TryGetReachableBuildingPathLength(
            currentBuilding.gameObject,
            enemyAI,
            movement,
            attackSlotManager,
            buildingMovePointSampleRange,
            out currentPathLength
        );

        float requiredAdvantage = Mathf.Max(0f, priorityTargetSwitchPathAdvantage);

        // 当前墙已经走不到，或者新墙路线至少短 requiredAdvantage 米时，才切换。
        // 현재 벽에 갈 수 없거나 새 벽 경로가 requiredAdvantage 미터 이상 짧을 때만 교체한다.
        if (!canReachCurrentBuilding || newPathLength + requiredAdvantage < currentPathLength)
        {
            SetPriorityTarget(newBuilding.gameObject);
        }
    }

    // 设置新优先目标；目标发生变化时释放旧建筑攻击点预约。
    // 새 우선 타깃을 설정하고, 타깃이 바뀌면 기존 건물 공격 지점 예약을 해제한다.
    void SetPriorityTarget(GameObject newTarget)
    {
        if (priorityTarget == newTarget)
        {
            return;
        }

        if (attackSlotManager != null)
        {
            attackSlotManager.ReleaseAttackPoint();
        }

        priorityTarget = newTarget;
        isAttackingBlockedWall = false;
        blockedWallOriginalTarget = null;
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

        float distance = IsRangedEnemy()
            ? GetRangedDistanceToTarget(core.gameObject)
            : GetDistanceToCore();

        if (distance <= attack.attackRange)
        {
            if (IsRangedEnemy())
            {
                movement.SetAutoRotation(false);
                FaceTarget(core.gameObject);
                attack.AttackRangedCore(core);
            }
            else
            {
                attack.AttackCore(core);
            }

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

        // 路径选择阶段已经验证过断口正前方的第一个物理障碍，这里直接使用绑定结果。
        // 경로 선택 단계에서 단절 지점 바로 앞의 첫 실제 장애물을 이미 검증했으므로 여기서는 연결된 결과를 바로 사용한다.
        DamageableBuilding blockingBuilding = pathChoice.blockingBuilding;

        // 只有物理验证未启用时才使用旧的大范围搜索，避免重新锁定入口旁边的迷惑墙。
        // 실제 장애물 검증이 비활성화된 경우에만 기존 넓은 범위 검색을 사용해서 입구 옆의 미끼 벽을 다시 선택하지 않게 한다.
        if (blockingBuilding == null &&
            pathChoice.blockerValidation == EnemyPathBlockHandler.BlockerValidationResult.NotChecked)
        {
            blockingBuilding = pathBlockHandler.FindBuildingBlockingPath(
                pathChoice.lastReachablePoint,
                pathChoice.destination,
                buildingLayer,
                blockedPointSearchRange,
                blockedPathForwardSearchRange
            );
        }

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

        // 远程型不占用建筑周围的近战攻击点，而是进入射程后直接攻击。
        // 원거리형은 건물 주변의 근접 공격 지점을 예약하지 않고 사거리 안에 들어오면 바로 공격한다.
        if (IsRangedEnemy())
        {
            MoveOrAttackRangedBuilding(building);
            return;
        }

        Vector3 movePoint;
        //敌人距离预约攻击点多近，才算“已经到达攻击点
        //적과 지정 공격 지점 사이의 거리가 일정 값 이하이면 공격 지점에 도착한 것으로 판단
        float preciseAttackPointArriveDistance = GetPreciseAttackPointArriveDistance();

        //攻击点管理器，请根据这座建筑和当前敌人的情况，
        // 尝试给我预约一个能走到、能攻击、没有被别人占用的位置，
        // 并把最终位置放进 movePoint(保存敌人应该前往的具体世界坐标)
        // 공격 지점 관리자에게 이 건물과 현재 적의 상태를 기준으로,
        // 이동 가능하고, 공격 가능하며, 다른 적이 점유하지 않은 위치를 예약하도록 요청한다.
        // 최종 위치는 movePoint에 저장한다. 
        // movePoint는 적이 이동해야 할 구체적인 월드 좌표를 의미한다.
        bool hasAttackPoint = attackSlotManager.TryReserveAttackPoint(
            building,
            enemyAI,
            movement,
            attackPointRefreshInterval,
            buildingMovePointSampleRange,
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

    // 远程敌人攻击建筑：不预约攻击点，先尝试从当前位置攻击，打不到时才寻路。
    // 원거리 적의 건물 공격: 공격 지점을 예약하지 않고 현재 위치에서 먼저 공격을 시도한 뒤, 공격할 수 없을 때만 길을 찾는다.
    void MoveOrAttackRangedBuilding(DamageableBuilding building)
    {
        if (building == null || building.hp <= 0f)
        {
            ClearPriorityTarget();
            return;
        }

        // 远程敌人不使用近战攻击点；如果之前曾经预约过，立即释放。
        // 원거리 적은 근접 공격 지점을 사용하지 않으므로 기존 예약이 있다면 즉시 해제한다.
        if (attackSlotManager != null)
        {
            attackSlotManager.ReleaseAttackPoint();
        }

        // 远程射程从真正的发射位置 FirePos 开始计算，不再使用近战敌人的身体半径和前方攻击盒。
        // 원거리 사거리는 실제 발사 위치 FirePos부터 계산하며, 근접 적의 몸 반지름이나 전방 공격 박스를 사용하지 않는다.
        movement.SetAutoRotation(false);
        FaceTarget(building.gameObject);

        float distanceToBuilding = GetRangedDistanceToTarget(building.gameObject);
        bool isInsideAttackRange = distanceToBuilding <= attack.attackRange + 0.05f;

        // 必须先检查射程，再检查路径。
        // NavMesh 路径不完整不代表远程攻击一定打不到目标。
        // 반드시 사거리를 먼저 확인하고 그다음 경로를 확인한다.
        // NavMesh 경로가 불완전해도 원거리 공격은 목표에 닿을 수 있다.
        if (isInsideAttackRange)
        {
            // 抛物线投射物会越过中间的敌人、环境墙和其他建筑，直接攻击当前锁定目标。
            // 포물선 투사체는 중간의 적, 환경 벽, 다른 건물을 넘어 현재 고정된 목표를 직접 공격한다.
            movement.Stop();
            attack.AttackRangedBuilding(building);
            return;
        }

        // 当前站位还打不到目标时，恢复 NavMeshAgent 自动转向并开始寻路。
        // 현재 위치에서 목표를 공격할 수 없으면 NavMeshAgent 자동 회전을 복구하고 길찾기를 시작한다.
        movement.SetAutoRotation(true);

        EnemyPathBlockHandler.PathChoice buildingPath;

        if (!pathBlockHandler.TryFindBestPathToTarget(
            building.gameObject,
            enemyAI,
            movement,
            navMeshSampleRange,
            targetPathPointExtraDistance,
            out buildingPath))
        {
            return;
        }

        // 目标还不在可攻击状态时，继续使用原来的堵路建筑识别和切换逻辑。
        // 아직 목표를 공격할 수 없으면 기존의 경로 차단 건물 식별 및 타깃 전환 로직을 계속 사용한다.
        if (HandleBlockedPath(buildingPath, building.gameObject))
        {
            return;
        }

        Vector3 rangedMovePoint;

        // 路径没有需要处理的堵路建筑后，再找一个“能够进入射程”的可达站位。
        // 这里只计算移动点，不会写入攻击点预约字典。
        // 처리할 경로 차단 건물이 없으면 사거리 안으로 들어갈 수 있는 도달 가능한 위치를 찾는다.
        // 여기서는 이동 위치만 계산하며 공격 지점 예약 Dictionary에는 기록하지 않는다.
        if (attackSlotManager != null &&
            attackSlotManager.TryGetReachableMovePointNearTarget(
                building.gameObject,
                enemyAI,
                movement,
                buildingMovePointSampleRange,
                out rangedMovePoint))
        {
            movement.MoveToPosition(
                rangedMovePoint,
                navMeshSampleRange,
                buildingAttackStoppingDistance
            );
            return;
        }

        // 找不到更合适的射击站位时，使用通用路径候选点作为最后兜底。
        // 더 적절한 사격 위치를 찾지 못하면 일반 경로 후보 지점을 마지막 대안으로 사용한다.
        movement.MoveToPosition(buildingPath.destination, navMeshSampleRange);
    }

    // 计算 FirePos 到目标 Collider 表面的距离；FirePos 未设置时暂时退回敌人自身位置。
    // FirePos에서 목표 Collider 표면까지의 거리를 계산하며, FirePos가 없으면 임시로 적 위치를 사용한다.
    float GetRangedDistanceToTarget(GameObject target)
    {
        Vector3 attackOrigin = attack != null && attack.firePos != null
            ? attack.firePos.position
            : transform.position;

        return EnemyTargetUtility.GetDistanceToTarget(attackOrigin, target);
    }

    bool IsRangedEnemy()
    {
        return
            enemyAI != null &&
            enemyAI.ClassTraits != null &&
            enemyAI.ClassTraits.enemyClass == EnemyAI.EnemyClass.Ranged;
    }

    // 建筑攻击点需要更精确，不能直接用普通 NavMeshAgent 停止距离
    // 건물 공격 위치는 더 정확해야 하므로 일반 NavMeshAgent 정지 거리만 사용하지 않음
    float GetPreciseAttackPointArriveDistance()
    {
        //获取敌人的半径 적의 반지름을 가져와
        float agentRadius = movement == null ? 0f : movement.Radius;
        //计算最低允许距离 최소 허용 거리 계산
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
