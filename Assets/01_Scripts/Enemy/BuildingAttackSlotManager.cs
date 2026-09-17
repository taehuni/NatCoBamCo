using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 建筑攻击点预约管理器：挂在敌人身上，负责为当前敌人预约某个建筑周围的攻击点。
// 건물 공격 지점 예약 관리자: 적에게 붙어서 현재 적이 특정 건물 주변의 공격 지점을 예약하도록 관리한다.
// 目标：避免很多敌人全部挤在同一个位置，同时让后面的敌人能在前面的敌人死亡后自动补位。
// 목적: 많은 적이 같은 위치에 겹치는 것을 줄이고, 앞의 적이 죽으면 뒤의 적이 자동으로 자리를 보충하게 한다.
public class BuildingAttackSlotManager : MonoBehaviour
{
    // 每个体型组预留一段独立编号，防止 normal[0] 和 large[0] 被误认为同一个攻击点。
    // 체형 그룹마다 독립된 번호 구간을 사용해서 normal[0]과 large[0]을 같은 지점으로 착각하지 않게 한다.
    private const int AttackPointGroupKeyRange = 100000;

    // 当前预约的是哪个建筑。GetInstanceID 可以给场景里的每个对象一个运行时唯一 ID。
    // 현재 예약한 건물이 무엇인지 저장한다. GetInstanceID는 씬 안의 각 오브젝트에 런타임 고유 ID를 준다.
    private int currentAttackBuildingId;
    // 当前预约的是这个建筑的第几个攻击点。-1 表示没有预约。
    // 현재 예약한 공격 지점의 인덱스다. -1이면 예약이 없다는 뜻이다.
    private int currentAttackPointIndex = -1;
    // 当前预约点的世界坐标。
    // 현재 예약 지점의 월드 좌표.
    private Vector3 currentAttackPoint;
    // 下次允许重新选择攻击点的时间，避免每一帧都疯狂换点。
    // 다음에 공격 지점을 다시 선택할 수 있는 시간. 매 프레임 계속 바꾸는 것을 막는다.
    private float nextAttackPointRefreshTime;

    // 当前预约的等待位置。等待位置位于攻击点外侧，避免没有攻击点的敌人继续挤进攻击区域。
    // 현재 예약한 대기 위치. 공격 지점 바깥에 배치해서 공격 지점이 없는 적이 공격 구역으로 계속 밀고 들어오지 않게 한다.
    private int currentWaitingBuildingId;
    private int currentWaitingPointIndex = -1;
    private Vector3 currentWaitingPoint;
    private float nextWaitingPointValidationTime;

    // 等待点不需要每帧重新算路，定期确认一次仍然可达即可。
    // 대기 지점은 매 프레임 경로를 다시 계산할 필요가 없으며 일정 간격으로 도달 가능 여부만 확인하면 된다.
    private const float WaitingPointValidationInterval = 0.5f;

    // 静态字典：所有敌人共享，用来记录“某个建筑的某个攻击点被哪个敌人占了”。
    // 정적 Dictionary: 모든 적이 공유하며, "어떤 건물의 어떤 공격 지점이 어느 적에게 예약되었는지" 기록한다.
    // 结构：建筑ID -> 攻击点Index -> 预约这个点的敌人SlotManager。
    // 구조: 건물ID -> 공격 지점 인덱스 -> 그 지점을 예약한 적의 SlotManager.
    private static Dictionary<int, Dictionary<int, BuildingAttackSlotManager>> attackPointReservations =
        new Dictionary<int, Dictionary<int, BuildingAttackSlotManager>>();

    // 等待点使用独立预约表，不会和真正的攻击点互相占用。
    // 대기 지점은 별도의 예약표를 사용하므로 실제 공격 지점 예약과 충돌하지 않는다.
    private static Dictionary<int, Dictionary<int, BuildingAttackSlotManager>> waitingPointReservations =
        new Dictionary<int, Dictionary<int, BuildingAttackSlotManager>>();

    // 当前敌人是否已经预约了一个攻击点。
    // 현재 적이 공격 지점을 예약했는지 여부.
    public bool HasAttackPoint
    {
        get
        {
            return currentAttackPointIndex >= 0;
        }
    }

    // 当前敌人预约到的攻击点位置。
    // 현재 적이 예약한 공격 지점 위치.
    public Vector3 CurrentAttackPoint
    {
        get
        {
            return currentAttackPoint;
        }
    }

    public int CurrentAttackBuildingId
    {
        get
        {
            return currentAttackBuildingId;
        }
    }

    public int CurrentAttackPointIndex
    {
        get
        {
            return currentAttackPointIndex;
        }
    }

    public bool HasWaitingPointFor(DamageableBuilding building)
    {
        return
            building != null &&
            currentWaitingPointIndex >= 0 &&
            currentWaitingBuildingId == building.GetInstanceID();
    }

    void OnDisable()
    {
        // 敌人被禁用时释放攻击点，避免点位永远被占。
        // 적이 비활성화될 때 공격 지점을 해제해서 자리가 영원히 점유되지 않게 한다.
        ReleaseAllReservations();
    }

    void OnDestroy()
    {
        // 敌人销毁时也释放攻击点。
        // 적이 제거될 때도 공격 지점을 해제한다.
        ReleaseAllReservations();
    }

    public bool TryReserveAttackPoint(
        DamageableBuilding building,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float refreshInterval,
        float buildingMovePointSampleRange,
        out Vector3 attackPoint)
    {
        attackPoint = transform.position;

        // 基础参数不完整时，释放旧预约并返回失败。
        // 기본 인자가 부족하면 기존 예약을 해제하고 실패를 반환한다.
        if (building == null || enemyAI == null || movement == null)
        {
            ReleaseAttackPoint();
            return false;
        }

        // 远程型敌人不使用建筑近战攻击点。这里作为保护，防止其他代码误调用预约入口。
        // 원거리형 적은 건물 근접 공격 지점을 사용하지 않는다. 다른 코드가 실수로 예약 입구를 호출하는 경우를 막는 보호 로직이다.
        if (enemyAI.ClassTraits != null &&
            enemyAI.ClassTraits.enemyClass == EnemyAI.EnemyClass.Ranged)
        {
            ReleaseAttackPoint();
            return false;
        }

        int buildingId = building.GetInstanceID();

        // 先清理这个建筑上已经失效的预约，例如敌人死亡后没有正常释放的情况。
        // 먼저 이 건물에 남아 있는 무효 예약을 정리한다. 예: 적이 죽었는데 정상 해제되지 않은 경우.
        CleanupAttackPointReservations(buildingId);

        // 记录当前敌人之前是不是已经预约了同一个建筑的点。
        // 현재 적이 이전에 같은 건물의 공격 지점을 예약했는지 기록한다.
        bool hadAttackPointOnSameBuilding =
            currentAttackBuildingId == buildingId && currentAttackPointIndex >= 0;

        int previousAttackPointIndex = currentAttackPointIndex;
        Vector3 previousAttackPoint = currentAttackPoint;

        // 如果还在刷新间隔内，继续使用原来的攻击点，不重新计算。
        // 아직 갱신 간격 안이면 기존 공격 지점을 계속 사용하고 다시 계산하지 않는다.
        if (currentAttackBuildingId == buildingId && currentAttackPointIndex >= 0)
        {
            if (Time.time < nextAttackPointRefreshTime)
            {
                attackPoint = currentAttackPoint;
                return true;
            }

            ReleaseAttackPoint();
        }

        // 如果之前预约的是别的建筑，先释放旧点。
        // 이전에 다른 건물을 예약했다면 먼저 기존 지점을 해제한다.
        if (currentAttackBuildingId != 0)
        {
            ReleaseAttackPoint();
        }

        // 拿到建筑周围所有候选攻击点：优先手动点，没有手动点就自动生成。
        // 건물 주변의 모든 후보 공격 지점을 가져온다. 수동 지점이 있으면 우선 사용하고, 없으면 자동 생성한다.
        int reservationKeyOffset;
        List<Vector3> attackPoints = GetAttackPointCandidates(
            building.gameObject,
            enemyAI,
            movement,
            out reservationKeyOffset
        );

        if (attackPoints.Count == 0)
        {
            return false;
        }

        int bestIndex = -1;
        Vector3 bestPoint = transform.position;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < attackPoints.Count; i++)
        {
            // 数组索引前加上体型组编号区间，得到这个点在共享预约表里的唯一编号。
            // 배열 인덱스 앞에 체형 그룹 번호 구간을 더해서 공유 예약표에서 사용할 고유 번호를 만든다.
            int reservationKey = reservationKeyOffset + i;

            // 已经被其他敌人预约的点跳过。
            // 다른 적이 이미 예약한 지점은 건너뛴다.
            if (IsAttackPointReservedByOtherEnemy(buildingId, reservationKey))
            {
                continue;
            }

            Vector3 navMeshPoint;

            // 攻击点可能不在 NavMesh 上，所以先修正到附近可走点。
            // 공격 지점이 NavMesh 위에 없을 수 있으므로 주변의 걸을 수 있는 지점으로 보정한다.
            if (!movement.TryGetNavMeshPoint(attackPoints[i], buildingMovePointSampleRange, out navMeshPoint))
            {
                continue;
            }

            NavMeshPath path;
            Vector3 lastReachablePoint;

            // 检查这个点是否真的能走到。
            // 이 지점까지 실제로 갈 수 있는지 확인한다.
            if (!movement.TryGetPath(navMeshPoint, buildingMovePointSampleRange, out path, out lastReachablePoint))
            {
                continue;
            }

            if (path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            // 计算到这个点的真实路径长度。
            // 이 지점까지의 실제 경로 길이를 계산한다.
            float pathLength = movement.GetPathLength(path);

            if (pathLength <= 0f)
            {
                pathLength = Vector3.Distance(transform.position, navMeshPoint);
            }

            float distanceToBuilding = EnemyTargetUtility.GetDistanceToTarget(navMeshPoint, building.gameObject);

            // 如果站在这个点也打不到建筑，就不要预约这个点。
            // 이 지점에 서도 건물을 공격할 수 없으면 예약하지 않는다.
            if (distanceToBuilding > GetBuildingAttackReach(enemyAI, movement) + 0.05f)
            {
                continue;
            }

            // 分数越低越优先：主要看路径长度，稍微考虑离建筑表面的距离。
            // 점수가 낮을수록 우선이다. 주로 경로 길이를 보고, 건물 표면까지의 거리도 조금 반영한다.
            float score = pathLength + distanceToBuilding * 0.2f;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = reservationKey;
                bestPoint = navMeshPoint;
            }
        }

        if (bestIndex < 0)
        {
            Vector3 previousNavMeshPoint;

            // 如果暂时找不到新点，但旧点仍然有效，就继续保留旧点，避免敌人频繁失去目标。
            // 새 지점을 찾지 못했지만 기존 지점이 아직 유효하면 유지해서 적이 자주 목표를 잃지 않게 한다.
            if (hadAttackPointOnSameBuilding && previousAttackPointIndex >= 0)
            {
                if (CanMoveToAttackPoint(
                    building,
                    enemyAI,
                    movement,
                    previousAttackPoint,
                    buildingMovePointSampleRange,
                    out previousNavMeshPoint))
                {
                    ReserveAttackPoint(buildingId, previousAttackPointIndex, previousNavMeshPoint, refreshInterval);
                    attackPoint = previousNavMeshPoint;
                    return true;
                }

                ReleaseAttackPoint();
            }

            return false;
        }

        // 预约最终选中的攻击点。
        // 최종 선택한 공격 지점을 예약한다.
        ReserveAttackPoint(buildingId, bestIndex, bestPoint, refreshInterval);
        attackPoint = bestPoint;
        return true;
    }

    // 没有抢到攻击点时，为敌人预约一个位于攻击点外侧的等待位置。
    // 공격 지점을 얻지 못했을 때 공격 지점 바깥쪽의 대기 위치를 예약한다.
    public bool TryReserveWaitingPoint(
        DamageableBuilding building,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float buildingMovePointSampleRange,
        out Vector3 waitingPoint)
    {
        waitingPoint = transform.position;

        if (building == null || enemyAI == null || movement == null || HasAttackPoint)
        {
            ReleaseWaitingPoint();
            return false;
        }

        int buildingId = building.GetInstanceID();
        CleanupWaitingPointReservations(buildingId);

        // 已经在等待同一座建筑时继续使用原位置，避免等待者来回换位。
        // 같은 건물을 기다리는 중이면 기존 위치를 유지해서 대기 중 좌우로 흔들리지 않게 한다.
        if (currentWaitingBuildingId == buildingId && currentWaitingPointIndex >= 0)
        {
            if (Time.time < nextWaitingPointValidationTime)
            {
                waitingPoint = currentWaitingPoint;
                return true;
            }

            Vector3 navMeshPoint;
            NavMeshPath path;
            Vector3 lastReachablePoint;

            if (movement.TryGetNavMeshPoint(currentWaitingPoint, buildingMovePointSampleRange, out navMeshPoint) &&
                movement.TryGetPath(navMeshPoint, buildingMovePointSampleRange, out path, out lastReachablePoint) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                currentWaitingPoint = navMeshPoint;
                nextWaitingPointValidationTime = Time.time + WaitingPointValidationInterval;
                waitingPoint = navMeshPoint;
                return true;
            }

            ReleaseWaitingPoint();
        }

        if (currentWaitingBuildingId != 0)
        {
            ReleaseWaitingPoint();
        }

        int reservationKeyOffset;
        List<Vector3> waitingPoints = GetWaitingPointCandidates(
            building.gameObject,
            enemyAI,
            movement,
            out reservationKeyOffset
        );

        int bestIndex = -1;
        Vector3 bestPoint = transform.position;
        float bestPathLength = Mathf.Infinity;
        float minimumWaitingDistance =
            GetBuildingAttackReach(enemyAI, movement) + Mathf.Max(0.2f, movement.Radius * 0.35f);

        for (int i = 0; i < waitingPoints.Count; i++)
        {
            int reservationKey = reservationKeyOffset + i;

            if (IsWaitingPointReservedByOtherEnemy(buildingId, reservationKey))
            {
                continue;
            }

            Vector3 navMeshPoint;

            if (!movement.TryGetNavMeshPoint(waitingPoints[i], buildingMovePointSampleRange, out navMeshPoint))
            {
                continue;
            }

            // NavMesh 修正后如果位置被吸回建筑攻击区域，就不把它当等待位置。
            // NavMesh 보정 후 건물 공격 구역 안으로 끌려온 위치는 대기 위치로 사용하지 않는다.
            if (EnemyTargetUtility.GetDistanceToTarget(navMeshPoint, building.gameObject) < minimumWaitingDistance)
            {
                continue;
            }

            NavMeshPath path;
            Vector3 lastReachablePoint;

            if (!movement.TryGetPath(navMeshPoint, buildingMovePointSampleRange, out path, out lastReachablePoint) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float pathLength = movement.GetPathLength(path);

            if (pathLength <= 0f)
            {
                pathLength = Vector3.Distance(transform.position, navMeshPoint);
            }

            if (pathLength < bestPathLength)
            {
                bestPathLength = pathLength;
                bestIndex = reservationKey;
                bestPoint = navMeshPoint;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        ReserveWaitingPoint(buildingId, bestIndex, bestPoint);
        waitingPoint = bestPoint;
        return true;
    }

    bool CanMoveToAttackPoint(
        DamageableBuilding building,
        EnemyAI enemyAI,
        EnemyMovement movement,
        Vector3 attackPoint,
        float buildingMovePointSampleRange,
        out Vector3 navMeshPoint)
    {
        navMeshPoint = attackPoint;

        // 用来检查“之前预约过的旧攻击点”现在是否仍然有效。
        // 이전에 예약했던 공격 지점이 지금도 유효한지 확인할 때 사용한다.
        if (building == null || enemyAI == null || movement == null)
        {
            return false;
        }

        if (!movement.TryGetNavMeshPoint(attackPoint, buildingMovePointSampleRange, out navMeshPoint))
        {
            return false;
        }

        NavMeshPath path;
        Vector3 lastReachablePoint;

        if (!movement.TryGetPath(navMeshPoint, buildingMovePointSampleRange, out path, out lastReachablePoint))
        {
            return false;
        }

        if (path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        float distanceToBuilding = EnemyTargetUtility.GetDistanceToTarget(navMeshPoint, building.gameObject);

        if (distanceToBuilding > GetBuildingAttackReach(enemyAI, movement) + 0.05f)
        {
            return false;
        }

        return true;
    }

    public bool CanAttackFromPoint(
        DamageableBuilding building,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float attackPointArriveDistance,
        LayerMask buildingLayer,
        float attackBoxWidth,
        float attackBoxHeight,
        Vector3 attackPoint,
        bool showDebug)
    {
        // 这个函数是“站在某个攻击点时能不能攻击”的完整判断。
        // 이 함수는 "어떤 공격 지점에 서 있을 때 공격 가능한지"를 전체적으로 판단한다.
        if (building == null || enemyAI == null || movement == null)
        {
            return false;
        }

        // 先判断敌人是否已经靠近预约点。
        // 먼저 적이 예약 지점에 충분히 가까운지 확인한다.
        if (!IsNearAttackPoint(movement, attackPoint, attackPointArriveDistance))
        {
            if (showDebug)
            {
                float distanceToAttackPoint = Vector3.Distance(transform.position, attackPoint);
                Debug.Log(gameObject.name + " is not near attack point. Distance: " + distanceToAttackPoint + ", required: " + attackPointArriveDistance);
            }

            return false;
        }

        // 到点后，再判断建筑是否进入敌人正前方攻击盒子。
        // 지점에 도착한 뒤, 건물이 적 정면 공격 박스 안에 있는지 확인한다.
        return IsBuildingInsideFrontAttackBox(building, enemyAI, buildingLayer, attackBoxWidth, attackBoxHeight, showDebug);
    }

    public bool IsBuildingInsideFrontAttackBox(
        DamageableBuilding targetBuilding,
        EnemyAI enemyAI,
        LayerMask buildingLayer,
        float attackBoxWidth,
        float attackBoxHeight,
        bool showDebug)
    {
        // 前方攻击盒检测：敌人不是用一个圆打四周，而是只检测自己面前的一块盒子区域。
        // 전방 공격 박스 판정: 적이 원형으로 사방을 때리는 것이 아니라, 자기 앞의 박스 영역만 검사한다.
        if (targetBuilding == null || enemyAI == null)
        {
            return false;
        }

        Vector3 boxCenter;
        Vector3 halfExtents;

        GetFrontAttackBox(enemyAI, attackBoxWidth, attackBoxHeight, out boxCenter, out halfExtents);

        // OverlapBox 找到前方盒子范围内的建筑 Collider。
        // OverlapBox로 전방 박스 범위 안의 건물 Collider를 찾는다.
        Collider[] hits = Physics.OverlapBox(
            boxCenter,
            halfExtents,
            transform.rotation,
            buildingLayer,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            DamageableBuilding building = hits[i].GetComponentInParent<DamageableBuilding>();

            if (showDebug)
            {
                string buildingName = building == null ? "No DamageableBuilding" : building.gameObject.name;
                Debug.Log(gameObject.name + " attack box hit collider: " + hits[i].gameObject.name + ", building: " + buildingName + ", target: " + targetBuilding.gameObject.name);
            }

            // 只有打到“当前目标建筑”才算攻击范围命中。
            // 현재 목표 건물에 맞았을 때만 공격 범위에 들어온 것으로 본다.
            if (building == targetBuilding)
            {
                return true;
            }
        }

        if (showDebug)
        {
            Debug.Log(gameObject.name + " attack box did not hit target building: " + targetBuilding.gameObject.name);
        }

        return false;
    }

    public bool HasClearAttackLineToBuilding(
        DamageableBuilding targetBuilding,
        EnemyAI enemyAI,
        LayerMask blockLayer,
        float lineRadius,
        bool showDebug)
    {
        // 攻击视线检测：防止敌人隔着其他敌人、环境墙、前排建筑打到目标。
        // 공격 시야 검사: 다른 적, 환경 벽, 앞쪽 건물 너머로 목표를 공격하는 것을 막는다.
        if (targetBuilding == null || enemyAI == null)
        {
            return false;
        }

        // 起点在敌人身体前方一点，避免射线从自己身体内部开始。
        // 시작점은 적 몸 앞쪽으로 조금 빼서, 자기 몸 안에서 검사선이 시작되는 것을 줄인다.
        Vector3 startPoint = GetAttackLineStartPoint(enemyAI);
        // 目标点使用建筑表面离起点最近的点。
        // 목표점은 건물 표면에서 시작점과 가장 가까운 점을 사용한다.
        Vector3 targetPoint = EnemyTargetUtility.GetClosestPointToTarget(startPoint, targetBuilding.gameObject);
        Vector3 direction = targetPoint - startPoint;
        float distance = direction.magnitude;

        if (distance <= 0.05f)
        {
            return true;
        }

        direction.Normalize();

        // SphereCastAll 相当于有粗细的射线，可以检测前方有没有东西挡住攻击。
        // SphereCastAll은 두께가 있는 레이캐스트처럼 동작해서 앞을 막는 물체가 있는지 검사한다.
        RaycastHit[] hits = Physics.SphereCastAll(
            startPoint,
            Mathf.Max(0.01f, lineRadius),
            direction,
            distance + 0.05f,
            blockLayer,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit nearestHit = new RaycastHit();
        bool hasNearestHit = false;
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            // 忽略敌人自己的碰撞体。
            // 적 자신의 Collider는 무시한다.
            if (hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            // 只记录离攻击起点最近的那个阻挡物。
            // 공격 시작점에서 가장 가까운 방해물만 기록한다.
            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                nearestHit = hits[i];
                hasNearestHit = true;
            }
        }

        if (!hasNearestHit)
        {
            if (showDebug)
            {
                Debug.Log(gameObject.name + " attack line hit nothing before target: " + targetBuilding.gameObject.name);
            }

            return false;
        }

        DamageableBuilding hitBuilding = nearestHit.collider.GetComponentInParent<DamageableBuilding>();

        if (showDebug)
        {
            string buildingName = hitBuilding == null ? "No DamageableBuilding" : hitBuilding.gameObject.name;
            Debug.Log(gameObject.name + " attack line first hit: " + nearestHit.collider.gameObject.name + ", building: " + buildingName + ", target: " + targetBuilding.gameObject.name);
        }

        // 最近打到的是目标建筑，才说明攻击线没有被挡住。
        // 가장 먼저 맞은 것이 목표 건물이어야 공격선이 막히지 않았다고 볼 수 있다.
        return hitBuilding == targetBuilding;
    }

    public bool IsNearAttackPoint(EnemyMovement movement, Vector3 attackPoint, float attackPointArriveDistance)
    {
        // 判断敌人是否已经到达预约攻击点附近。
        // 적이 예약한 공격 지점 근처에 도착했는지 판단한다.
        float distanceToAttackPoint = Vector3.Distance(transform.position, attackPoint);
        float allowedDistanceToPoint = Mathf.Max(0.05f, attackPointArriveDistance);

        return distanceToAttackPoint <= allowedDistanceToPoint;
    }

    public bool TryGetReachableMovePointNearTarget(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float buildingMovePointSampleRange,
        out Vector3 movePoint)
    {
        movePoint = transform.position;

        // 给目标找一个可走到的靠近点，主要用于“粗略靠近建筑”。
        // 타깃 근처에서 갈 수 있는 지점을 찾는다. 주로 "건물 근처로 대략 이동"할 때 사용한다.
        if (target == null || enemyAI == null || movement == null || !movement.IsReady)
        {
            return false;
        }

        // 从目标表面最近点向敌人方向外推，得到一个站位方向。
        // 타깃 표면의 가장 가까운 점에서 적 방향으로 바깥쪽을 향하는 서기 방향을 구한다.
        Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, target);
        Vector3 awayDirection = transform.position - closestPoint;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = transform.position - target.transform.position;
            awayDirection.y = 0f;
        }

        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = -transform.forward;
        }

        awayDirection.Normalize();

        Vector3 sideDirection = Vector3.Cross(Vector3.up, awayDirection).normalized;
        Vector3 leftDiagonal = (awayDirection + sideDirection).normalized;
        Vector3 rightDiagonal = (awayDirection - sideDirection).normalized;

        float enemyAttackRange = GetEnemyAttackRange(enemyAI);
        float standDistance = Mathf.Max(enemyAttackRange * 0.8f, movement.Radius + 0.3f);

        // 生成正面、斜侧、左右几个候选站位点。
        // 정면, 대각선, 좌우 방향의 후보 위치를 만든다.
        Vector3[] candidatePoints =
        {
            closestPoint + awayDirection * standDistance,
            closestPoint + leftDiagonal * standDistance,
            closestPoint + rightDiagonal * standDistance,
            closestPoint + sideDirection * standDistance,
            closestPoint - sideDirection * standDistance
        };

        bool foundPoint = false;
        float shortestPathLength = Mathf.Infinity;

        for (int i = 0; i < candidatePoints.Length; i++)
        {
            Vector3 navMeshPoint;

            // 候选点先修正到 NavMesh 上。
            // 후보 지점을 먼저 NavMesh 위로 보정한다.
            if (!movement.TryGetNavMeshPoint(candidatePoints[i], buildingMovePointSampleRange, out navMeshPoint))
            {
                continue;
            }

            NavMeshPath path;
            Vector3 lastReachablePoint;

            if (!movement.TryGetPath(navMeshPoint, buildingMovePointSampleRange, out path, out lastReachablePoint))
            {
                continue;
            }

            // 只接受完整可达路径。
            // 완전히 도달 가능한 경로만 허용한다.
            if (path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float pathLength = movement.GetPathLength(path);

            if (pathLength <= 0f)
            {
                pathLength = Vector3.Distance(transform.position, navMeshPoint);
            }

            // 选路径最短的靠近点。
            // 경로가 가장 짧은 접근 지점을 선택한다.
            if (pathLength < shortestPathLength)
            {
                shortestPathLength = pathLength;
                movePoint = navMeshPoint;
                foundPoint = true;
            }
        }

        return foundPoint;
    }

    public Vector3 GetMovePointNearTarget(GameObject target, EnemyMovement movement, float buildingMovePointSampleRange)
    {
        Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, target);

        Vector3 navMeshPoint;

        if (movement != null && movement.TryGetNavMeshPoint(closestPoint, buildingMovePointSampleRange, out navMeshPoint))
        {
            return navMeshPoint;
        }

        return closestPoint;
    }

    public void ReleaseAttackPoint()
    {
        // 没有预约点时不需要释放。
        // 예약한 지점이 없으면 해제할 필요가 없다.
        if (currentAttackBuildingId == 0 || currentAttackPointIndex < 0)
        {
            return;
        }

        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        // 从共享字典中移除“当前敌人占用的点”。
        // 공유 Dictionary에서 "현재 적이 점유한 지점"을 제거한다.
        if (attackPointReservations.TryGetValue(currentAttackBuildingId, out buildingReservations))
        {
            BuildingAttackSlotManager reservedEnemy;

            if (buildingReservations.TryGetValue(currentAttackPointIndex, out reservedEnemy) && reservedEnemy == this)
            {
                buildingReservations.Remove(currentAttackPointIndex);
            }

            if (buildingReservations.Count == 0)
            {
                attackPointReservations.Remove(currentAttackBuildingId);
            }
        }

        // 清空本敌人自己的预约记录。
        // 이 적 자신의 예약 기록을 초기화한다.
        currentAttackBuildingId = 0;
        currentAttackPointIndex = -1;
        currentAttackPoint = Vector3.zero;
        nextAttackPointRefreshTime = 0f;
    }

    public void ReleaseWaitingPoint()
    {
        if (currentWaitingBuildingId == 0 || currentWaitingPointIndex < 0)
        {
            return;
        }

        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        if (waitingPointReservations.TryGetValue(currentWaitingBuildingId, out buildingReservations))
        {
            BuildingAttackSlotManager reservedEnemy;

            if (buildingReservations.TryGetValue(currentWaitingPointIndex, out reservedEnemy) &&
                reservedEnemy == this)
            {
                buildingReservations.Remove(currentWaitingPointIndex);
            }

            if (buildingReservations.Count == 0)
            {
                waitingPointReservations.Remove(currentWaitingBuildingId);
            }
        }

        currentWaitingBuildingId = 0;
        currentWaitingPointIndex = -1;
        currentWaitingPoint = Vector3.zero;
        nextWaitingPointValidationTime = 0f;
    }

    // 目标变化、敌人禁用或销毁时，同时释放攻击点和等待点。
    // 타깃 변경, 적 비활성화 또는 제거 시 공격 지점과 대기 지점을 함께 해제한다.
    public void ReleaseAllReservations()
    {
        ReleaseAttackPoint();
        ReleaseWaitingPoint();
    }

    public void DrawCurrentAttackPointGizmos(float attackPointArriveDistance)
    {
        if (currentAttackPointIndex < 0)
        {
            if (currentWaitingPointIndex >= 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, currentWaitingPoint);
                Gizmos.DrawWireSphere(currentWaitingPoint, 0.18f);
            }

            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, currentAttackPoint);
        Gizmos.DrawSphere(currentAttackPoint, 0.15f);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(currentAttackPoint, attackPointArriveDistance);
    }

    public void DrawFrontAttackBoxGizmos(EnemyAI enemyAI, float attackBoxWidth, float attackBoxHeight)
    {
        if (enemyAI == null)
        {
            return;
        }

        Vector3 boxCenter;
        Vector3 halfExtents;

        GetFrontAttackBox(enemyAI, attackBoxWidth, attackBoxHeight, out boxCenter, out halfExtents);

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

        Gizmos.matrix = oldMatrix;
    }

    void ReserveAttackPoint(int buildingId, int attackPointIndex, Vector3 attackPoint, float refreshInterval)
    {
        // 一旦获得真正的攻击点，就不再占用外围等待点。
        // 실제 공격 지점을 얻으면 바깥쪽 대기 지점은 더 이상 점유하지 않는다.
        ReleaseWaitingPoint();

        // 如果预约的还是同一个点，只更新坐标和下次刷新时间。
        // 같은 지점을 계속 예약 중이면 좌표와 다음 갱신 시간만 갱신한다.
        if (currentAttackBuildingId == buildingId && currentAttackPointIndex == attackPointIndex)
        {
            currentAttackPoint = attackPoint;
            nextAttackPointRefreshTime = Time.time + refreshInterval;
            return;
        }

        // 预约新点前，先释放旧点。
        // 새 지점을 예약하기 전에 기존 지점을 먼저 해제한다.
        ReleaseAttackPoint();

        // 如果这个建筑还没有预约表，就创建一个。
        // 이 건물의 예약표가 없으면 새로 만든다.
        if (!attackPointReservations.ContainsKey(buildingId))
        {
            attackPointReservations[buildingId] = new Dictionary<int, BuildingAttackSlotManager>();
        }

        // 在共享字典中记录：这个建筑的这个攻击点被当前敌人占用。
        // 공유 Dictionary에 이 건물의 이 공격 지점이 현재 적에게 점유되었다고 기록한다.
        attackPointReservations[buildingId][attackPointIndex] = this;

        // 同步本敌人的本地预约数据。
        // 현재 적의 로컬 예약 데이터도 같이 갱신한다.
        currentAttackBuildingId = buildingId;
        currentAttackPointIndex = attackPointIndex;
        currentAttackPoint = attackPoint;
        nextAttackPointRefreshTime = Time.time + refreshInterval;
    }

    void ReserveWaitingPoint(int buildingId, int waitingPointIndex, Vector3 waitingPoint)
    {
        if (currentWaitingBuildingId == buildingId && currentWaitingPointIndex == waitingPointIndex)
        {
            currentWaitingPoint = waitingPoint;
            return;
        }

        ReleaseWaitingPoint();

        if (!waitingPointReservations.ContainsKey(buildingId))
        {
            waitingPointReservations[buildingId] = new Dictionary<int, BuildingAttackSlotManager>();
        }

        waitingPointReservations[buildingId][waitingPointIndex] = this;
        currentWaitingBuildingId = buildingId;
        currentWaitingPointIndex = waitingPointIndex;
        currentWaitingPoint = waitingPoint;
        nextWaitingPointValidationTime = Time.time + WaitingPointValidationInterval;
    }

    bool IsWaitingPointReservedByOtherEnemy(int buildingId, int waitingPointIndex)
    {
        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        if (!waitingPointReservations.TryGetValue(buildingId, out buildingReservations))
        {
            return false;
        }

        BuildingAttackSlotManager reservedEnemy;

        if (!buildingReservations.TryGetValue(waitingPointIndex, out reservedEnemy))
        {
            return false;
        }

        bool isInvalid =
            reservedEnemy == null ||
            !reservedEnemy.isActiveAndEnabled ||
            reservedEnemy.currentWaitingBuildingId != buildingId ||
            reservedEnemy.currentWaitingPointIndex != waitingPointIndex;

        if (isInvalid)
        {
            buildingReservations.Remove(waitingPointIndex);
            return false;
        }

        return reservedEnemy != this;
    }

    bool IsAttackPointReservedByOtherEnemy(int buildingId, int attackPointIndex)
    {
        // 判断某个点是否已经被“其他敌人”预约。
        // 특정 지점이 "다른 적"에게 이미 예약되었는지 판단한다.
        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        // 没有这个建筑的预约记录，说明没人占。
        // 이 건물의 예약 기록이 없으면 아무도 점유하지 않은 것이다.
        if (!attackPointReservations.TryGetValue(buildingId, out buildingReservations))
        {
            return false;
        }

        BuildingAttackSlotManager reservedEnemy;

        // 这个点没有记录，说明没人占。
        // 이 지점에 기록이 없으면 아무도 점유하지 않은 것이다.
        if (!buildingReservations.TryGetValue(attackPointIndex, out reservedEnemy))
        {
            return false;
        }

        // 如果记录里的敌人已经没了或者被禁用，就清掉这个无效预约。
        // 기록된 적이 사라졌거나 비활성화되었으면 무효 예약을 제거한다.
        if (reservedEnemy == null ||
            !reservedEnemy.isActiveAndEnabled ||
            reservedEnemy.currentAttackBuildingId != buildingId ||
            reservedEnemy.currentAttackPointIndex != attackPointIndex)
        {
            buildingReservations.Remove(attackPointIndex);
            return false;
        }

        // 只要预约者不是自己，就说明这个点被其他敌人占了。
        // 예약자가 자기 자신이 아니면 다른 적이 점유한 것이다.
        return reservedEnemy != this;
    }

    void CleanupAttackPointReservations(int buildingId)
    {
        // 清理某个建筑上已经失效的预约。
        // 특정 건물에 남아 있는 무효 예약을 정리한다.
        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        if (!attackPointReservations.TryGetValue(buildingId, out buildingReservations))
        {
            return;
        }

        List<int> removeIndexes = null;

        foreach (KeyValuePair<int, BuildingAttackSlotManager> reservation in buildingReservations)
        {
            BuildingAttackSlotManager reservedEnemy = reservation.Value;

            // 预约敌人还存在且启用，就保留。
            // 예약한 적이 아직 존재하고 활성화되어 있으면 유지한다.
            if (reservedEnemy != null &&
                reservedEnemy.isActiveAndEnabled &&
                reservedEnemy.currentAttackBuildingId == buildingId &&
                reservedEnemy.currentAttackPointIndex == reservation.Key)
            {
                continue;
            }

            // 不能在 foreach 里直接 Remove，所以先把要删除的 index 记下来。
            // foreach 중에는 바로 Remove할 수 없으므로 삭제할 index를 먼저 기록한다.
            if (removeIndexes == null)
            {
                removeIndexes = new List<int>();
            }

            removeIndexes.Add(reservation.Key);
        }

        // 统一删除失效预约。
        // 무효 예약을 한 번에 삭제한다.
        if (removeIndexes != null)
        {
            for (int i = 0; i < removeIndexes.Count; i++)
            {
                buildingReservations.Remove(removeIndexes[i]);
            }
        }

        // 如果这个建筑已经没有任何预约，就把整个建筑记录也删掉。
        // 이 건물에 예약이 하나도 남지 않으면 건물 기록 자체도 제거한다.
        if (buildingReservations.Count == 0)
        {
            attackPointReservations.Remove(buildingId);
        }
    }

    void CleanupWaitingPointReservations(int buildingId)
    {
        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        if (!waitingPointReservations.TryGetValue(buildingId, out buildingReservations))
        {
            return;
        }

        List<int> removeIndexes = null;

        foreach (KeyValuePair<int, BuildingAttackSlotManager> reservation in buildingReservations)
        {
            BuildingAttackSlotManager reservedEnemy = reservation.Value;

            if (reservedEnemy != null &&
                reservedEnemy.isActiveAndEnabled &&
                reservedEnemy.currentWaitingBuildingId == buildingId &&
                reservedEnemy.currentWaitingPointIndex == reservation.Key)
            {
                continue;
            }

            if (removeIndexes == null)
            {
                removeIndexes = new List<int>();
            }

            removeIndexes.Add(reservation.Key);
        }

        if (removeIndexes != null)
        {
            for (int i = 0; i < removeIndexes.Count; i++)
            {
                buildingReservations.Remove(removeIndexes[i]);
            }
        }

        if (buildingReservations.Count == 0)
        {
            waitingPointReservations.Remove(buildingId);
        }
    }

    void GetFrontAttackBox(EnemyAI enemyAI, float attackBoxWidth, float attackBoxHeight, out Vector3 boxCenter, out Vector3 halfExtents)
    {
        // 攻击盒子的长度使用敌人的攻击距离。
        // 공격 박스의 길이는 적의 공격 거리를 사용한다.
        float length = Mathf.Max(0.1f, GetEnemyAttackRange(enemyAI));
        float width = Mathf.Max(0.1f, attackBoxWidth);
        float height = Mathf.Max(0.1f, attackBoxHeight);
        // 从敌人身体前方开始算，避免攻击盒子大量覆盖自己身体后方。
        // 적 몸 앞쪽부터 계산해서 공격 박스가 자기 몸 뒤쪽까지 많이 덮는 것을 줄인다.
        float bodyForwardOffset = GetBodyForwardOffset(enemyAI);

        // 盒子中心 = 敌人位置 + 前方偏移 + 高度偏移。
        // 박스 중심 = 적 위치 + 전방 오프셋 + 높이 오프셋.
        boxCenter =
            transform.position +
            transform.forward * (bodyForwardOffset + length * 0.5f) +
            Vector3.up * (height * 0.5f);

        // OverlapBox 需要的是半尺寸，所以宽高长都乘 0.5。
        // OverlapBox는 반 크기를 받으므로 너비/높이/길이에 0.5를 곱한다.
        halfExtents = new Vector3(
            width * 0.5f,
            height * 0.5f,
            length * 0.5f
        );
    }

    float GetBuildingAttackReach(EnemyAI enemyAI, EnemyMovement movement)
    {
        if (enemyAI == null)
        {
            return 0f;
        }

        float bodyRadius = movement == null ? 0f : movement.Radius;
        return GetEnemyAttackRange(enemyAI) + bodyRadius;
    }

    float GetEnemyAttackRange(EnemyAI enemyAI)
    {
        if (enemyAI == null || enemyAI.AttackModule == null)
        {
            return 0f;
        }

        return enemyAI.AttackModule.attackRange;
    }

    float GetBodyForwardOffset(EnemyAI enemyAI)
    {
        if (enemyAI == null)
        {
            return 0f;
        }

        EnemyMovement movement = enemyAI.GetComponent<EnemyMovement>();

        if (movement == null)
        {
            return 0f;
        }

        return movement.Radius;
    }

    Vector3 GetAttackLineStartPoint(EnemyAI enemyAI)
    {
        EnemyMovement movement = enemyAI.GetComponent<EnemyMovement>();
        float bodyForwardOffset = movement == null ? 0f : movement.Radius;
        float height = 0.5f;

        if (movement != null && movement.Agent != null)
        {
            height = Mathf.Clamp(movement.Agent.height * 0.5f, 0.3f, 1.5f);
        }

        return
            transform.position +
            transform.forward * bodyForwardOffset +
            Vector3.up * height;
    }

    List<Vector3> GetAttackPointCandidates(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        out int reservationKeyOffset)
    {
        // 最终返回的候选攻击点列表。
        // 최종 반환할 후보 공격 지점 목록.
        List<Vector3> attackPoints = new List<Vector3>();

        // 不同体型使用不同的预约编号区间，避免不同数组中相同 index 互相冲突。
        // 체형마다 다른 예약 번호 구간을 사용해서 서로 다른 배열의 같은 index가 충돌하지 않게 한다.
        BuildingAttackPoints.AttackPointGroup attackPointGroup =
            BuildingAttackPoints.GetAttackPointGroupForEnemy(enemyAI);
        reservationKeyOffset = (int)attackPointGroup * AttackPointGroupKeyRange;

        // 优先读取建筑上手动配置的攻击点。
        // 먼저 건물에 수동으로 배치한 공격 지점을 읽는다.
        BuildingAttackPoints manualAttackPoints = target.GetComponentInChildren<BuildingAttackPoints>();

        Transform[] selectedAttackPoints = null;

        if (manualAttackPoints != null)
        {
            // 根据当前敌人的类型取得普通组或大型组。Boss 分支目前保留为注释。
            // 현재 적 타입에 따라 일반 그룹 또는 대형 그룹을 가져온다. Boss 분기는 현재 주석으로 남겨 둔다.
            selectedAttackPoints = manualAttackPoints.GetAttackPointsForEnemy(enemyAI);
        }

        if (selectedAttackPoints != null)
        {
            for (int i = 0; i < selectedAttackPoints.Length; i++)
            {
                if (selectedAttackPoints[i] == null)
                {
                    continue;
                }

                // Transform.position 是这个攻击点在世界空间中的位置。
                // Transform.position은 이 공격 지점의 월드 좌표다.
                attackPoints.Add(selectedAttackPoints[i].position);
            }
        }

        // 如果有手动点，直接使用手动点。手动点通常比自动猜点更稳定。
        // 수동 지점이 있으면 그대로 사용한다. 수동 지점은 자동 계산보다 보통 더 안정적이다.
        if (attackPoints.Count > 0)
        {
            return attackPoints;
        }

        // 没有手动点时，再根据建筑 Bounds 自动生成候选点。
        // 수동 지점이 없을 때만 건물 Bounds를 기준으로 후보 지점을 자동 생성한다.
        AddAutoAttackPointCandidates(target, enemyAI, movement, attackPoints);
        return attackPoints;
    }

    List<Vector3> GetWaitingPointCandidates(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        out int reservationKeyOffset)
    {
        int attackPointGroupOffset;
        List<Vector3> attackPoints = GetAttackPointCandidates(
            target,
            enemyAI,
            movement,
            out attackPointGroupOffset
        );

        reservationKeyOffset = attackPointGroupOffset;
        List<Vector3> waitingPoints = new List<Vector3>();

        Bounds bounds;
        Vector3 targetCenter = target.transform.position;

        if (EnemyTargetUtility.TryGetTargetBounds(target, out bounds))
        {
            targetCenter = bounds.center;
        }

        targetCenter.y = transform.position.y;
        float rowSpacing = Mathf.Max(movement.Radius * 2.5f + 0.5f, 1.5f);
        const int waitingRowCount = 3;

        // 每个攻击点后方生成三层等待点。越靠后的敌人会停在更外侧，不再全挤到攻击区域。
        // 각 공격 지점 뒤에 세 줄의 대기 지점을 만든다. 뒤쪽 적은 더 바깥에서 기다려 공격 구역에 몰리지 않는다.
        for (int row = 1; row <= waitingRowCount; row++)
        {
            for (int i = 0; i < attackPoints.Count; i++)
            {
                Vector3 outwardDirection = attackPoints[i] - targetCenter;
                outwardDirection.y = 0f;

                if (outwardDirection.sqrMagnitude < 0.001f)
                {
                    outwardDirection = transform.position - targetCenter;
                    outwardDirection.y = 0f;
                }

                if (outwardDirection.sqrMagnitude < 0.001f)
                {
                    outwardDirection = Vector3.back;
                }

                outwardDirection.Normalize();
                waitingPoints.Add(attackPoints[i] + outwardDirection * rowSpacing * row);
            }
        }

        return waitingPoints;
    }

    void AddAutoAttackPointCandidates(GameObject target, EnemyAI enemyAI, EnemyMovement movement, List<Vector3> attackPoints)
    {
        Bounds bounds;

        // 如果目标没有 Bounds，就退回到目标附近一个可走点。
        // 타깃 Bounds를 얻지 못하면 타깃 근처의 걸을 수 있는 지점 하나로 대체한다.
        if (!EnemyTargetUtility.TryGetTargetBounds(target, out bounds))
        {
            attackPoints.Add(GetMovePointNearTarget(target, movement, 2f));
            return;
        }

        // 敌人站在建筑外侧的距离。
        // 적이 건물 바깥쪽에 서야 하는 거리.
        float enemyAttackRange = GetEnemyAttackRange(enemyAI);
        float standDistance = Mathf.Max(enemyAttackRange * 0.8f, movement.Radius + 0.4f);
        // 攻击点之间的间隔。敌人半径越大，点位间隔越大。
        // 공격 지점 사이의 간격. 적 반지름이 클수록 지점 간격도 커진다.
        float pointSpacing = Mathf.Max(movement.Radius * 2.5f, 1.2f);
        float y = transform.position.y;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        // 在建筑 Bounds 的四条边外侧生成一圈候选攻击点。
        // 건물 Bounds의 네 변 바깥쪽에 후보 공격 지점을 한 바퀴 생성한다.
        AddAttackPointsOnLine(attackPoints, new Vector3(min.x, y, min.z - standDistance), new Vector3(max.x, y, min.z - standDistance), pointSpacing);
        AddAttackPointsOnLine(attackPoints, new Vector3(min.x, y, max.z + standDistance), new Vector3(max.x, y, max.z + standDistance), pointSpacing);
        AddAttackPointsOnLine(attackPoints, new Vector3(min.x - standDistance, y, min.z), new Vector3(min.x - standDistance, y, max.z), pointSpacing);
        AddAttackPointsOnLine(attackPoints, new Vector3(max.x + standDistance, y, min.z), new Vector3(max.x + standDistance, y, max.z), pointSpacing);
    }

    void AddAttackPointsOnLine(List<Vector3> attackPoints, Vector3 startPoint, Vector3 endPoint, float pointSpacing)
    {
        // 计算一条边的长度，然后根据间距决定生成多少个点。
        // 한 변의 길이를 계산하고, 간격에 따라 몇 개의 지점을 만들지 결정한다.
        float length = Vector3.Distance(startPoint, endPoint);
        int pointCount = Mathf.Max(1, Mathf.CeilToInt(length / pointSpacing) + 1);

        for (int i = 0; i < pointCount; i++)
        {
            // t 从 0 到 1，表示从起点到终点的插值比例。
            // t는 0부터 1까지이며, 시작점에서 끝점까지의 보간 비율이다.
            float t = pointCount == 1 ? 0.5f : (float)i / (pointCount - 1);
            // Vector3.Lerp 根据 t 在起点和终点之间生成一个点。
            // Vector3.Lerp는 t 비율에 따라 시작점과 끝점 사이의 점을 만든다.
            attackPoints.Add(Vector3.Lerp(startPoint, endPoint, t));
        }
    }
}
