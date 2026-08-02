using UnityEngine;
using UnityEngine.AI;

// 敌人路径堵塞处理模块：负责判断路径是否完整，以及在路径不完整时寻找堵路建筑。
// 적 경로 막힘 처리 모듈: 경로가 완전한지 판단하고, 경로가 막혔을 때 막고 있는 건물을 찾는다.
// 它只负责“路径分析”，不负责真正移动或者攻击。
// 이 모듈은 "경로 분석"만 담당하고, 실제 이동이나 공격은 담당하지 않는다.

// 路径堵塞处理模块：负责判断路径是否完整、选择最佳路径点、寻找堵路建筑。
// 경로 막힘 처리 모듈: 경로 완성 여부 판단, 최적 경로 지점 선택, 길을 막는 건물 찾기를 담당함.
public class EnemyPathBlockHandler : MonoBehaviour
{
    [Header("Physical Blocker Validation / 실제 장애물 검증")]
    public LayerMask pathBlockerLayer;
    public float pathBlockerCastRadius = 0.15f;
    public float pathBlockerCastBackDistance = 0.4f;
    public float pathBlockerCastForwardDistance = 2.5f;
    public float manualPathPointMaxSnapDistance = 0.5f;
    public bool showPathBlockerDebug;

    // 不完整路径前方物理障碍的验证结果。
    // 불완전 경로 앞쪽의 실제 장애물 검증 결과.
    public enum BlockerValidationResult
    {
        NotChecked,
        NoPhysicalBlocker,
        BreakableBuilding,
        UnbreakableObstacle
    }

    // 一次寻路结果的数据包。
    // 한 번의 길찾기 결과를 담는 데이터 패키지.
    public struct PathChoice
    {
        // 是否成功生成路径报告，不代表路径一定完整。
        // 경로 보고서를 만들었는지 여부. 경로가 반드시 완성되었다는 뜻은 아님.
        public bool hasPath;
        // 路径是否能完整到达目标。
        // 목표까지 경로가 완전히 이어지는지 여부.
        public bool isComplete;
        // 修正到 NavMesh 上后的目标点。
        // NavMesh 위로 보정된 목표 지점.
        public Vector3 destination;
        // 修正前真正请求的候选点，用来判断 SamplePosition 是否把点移得太远。
        // 보정 전 실제로 요청한 후보 지점. SamplePosition이 지점을 너무 멀리 옮겼는지 판단할 때 사용.
        public Vector3 requestedDestination;
        // 原始候选点到 NavMesh 修正点之间的水平距离。
        // 원래 후보 지점과 NavMesh 보정 지점 사이의 수평 거리.
        public float destinationSnapDistance;
        // 手动候选点是否被修正得太远，通常表示该点被建筑的 Carve 区域覆盖。
        // 수동 후보 지점이 너무 멀리 보정되었는지 여부. 보통 건물의 Carve 영역에 덮였음을 뜻함.
        public bool destinationWasSnappedTooFar;
        // 这条路径最后能走到的位置。
        // 이 경로에서 마지막으로 도달 가능한 위치.
        public Vector3 lastReachablePoint;
        // 从当前位置沿 NavMesh 走到 lastReachablePoint 的路径长度。
        // 현재 위치에서 NavMesh를 따라 lastReachablePoint까지 가는 경로 길이.
        public float pathLength;
        // 用来比较路线优劣的分数，越小越优先。
        // 경로 우선순위를 비교하기 위한 점수. 낮을수록 우선.
        public float score;
        // 路径最后一段的前进方向，用来从路径断口继续向前验证真正的障碍物。
        // 경로 마지막 구간의 진행 방향. 경로가 끊긴 지점 앞의 실제 장애물을 확인할 때 사용.
        public Vector3 approachDirection;
        // 当前不完整路径的物理障碍验证状态。
        // 현재 불완전 경로의 실제 장애물 검증 상태.
        public BlockerValidationResult blockerValidation;
        // 如果断口前方第一个障碍是可破坏建筑，就保存在这里。
        // 끊긴 지점 앞 첫 장애물이 파괴 가능한 건물이면 여기에 저장.
        public DamageableBuilding blockingBuilding;
        // 物理检测实际命中的位置，主要用于调试观察。
        // 실제 물리 검사가 맞은 위치. 주로 디버그 확인용.
        public Vector3 blockerHitPoint;
    }

    void Awake()
    {
        SetupDefaultPathBlockerLayer();
    }

    // 判断去 destination 的路径是否完整，同时把路径报告通过 out 返回。
    // destination까지의 경로가 완성인지 판단하고, 경로 보고서를 out으로 반환.
    public bool IsPathComplete(
        Vector3 destination,
        EnemyMovement movement,
        float navMeshSampleRange,
        out PathChoice pathChoice)
    {
        // 先生成完整 PathChoice 报告，生成失败就不能认为路径完整。
        // 먼저 전체 PathChoice 보고서를 만들고, 실패하면 경로가 완성되었다고 볼 수 없음.
        if (!TryBuildPathChoice(destination, movement, navMeshSampleRange, out pathChoice))
        {
            return false;
        }

        // 真正判断路径是否完整的是 pathChoice.isComplete。
        // 실제 경로 완성 여부는 pathChoice.isComplete로 판단.
        return pathChoice.isComplete;
    }

    // 给一个目标生成多个候选点，分别算路，然后选 score 最低的一条路径。
    // 하나의 목표 주변에 여러 후보 지점을 만들고 각각 경로를 계산한 뒤 score가 가장 낮은 경로를 선택.
    public bool TryFindBestPathToTarget(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float navMeshSampleRange,
        float targetPathPointExtraDistance,
        out PathChoice bestPath)
    {
        // out 参数必须先赋值，先准备一个空结果。
        // out 파라미터는 반드시 값을 가져야 하므로 먼저 빈 결과를 준비.
        bestPath = new PathChoice();

        if (target == null || enemyAI == null || movement == null)
        {
            return false;
        }

        // 如果目标配置了手动候选点，优先使用设计者摆放的位置。
        // 목표에 수동 후보 지점이 설정되어 있으면 디자이너가 배치한 위치를 우선 사용한다.
        Vector3[] manualCandidatePoints;

        if (TryGetManualPathCandidatePoints(target, out manualCandidatePoints))
        {
            if (TryFindBestPathFromCandidatePoints(
                manualCandidatePoints,
                movement,
                navMeshSampleRange,
                Mathf.Max(0f, manualPathPointMaxSnapDistance),
                out bestPath))
            {
                return true;
            }
        }

        // 没有有效手动点，或者手动点全部无法生成路径时，使用原来的自动候选点兜底。
        // 유효한 수동 지점이 없거나 모든 수동 지점의 경로 계산이 실패하면 기존 자동 후보 지점을 사용한다.
        Vector3[] automaticCandidatePoints = GetAutomaticPathCandidatePoints(
            target,
            enemyAI,
            movement,
            targetPathPointExtraDistance
        );

        return TryFindBestPathFromCandidatePoints(
            automaticCandidatePoints,
            movement,
            navMeshSampleRange,
            Mathf.Infinity,
            out bestPath
        );
    }

    // 从一组候选点中分别计算路径，并返回 score 最低的一条。
    // 후보 지점 목록의 경로를 각각 계산하고 score가 가장 낮은 경로를 반환한다.
    bool TryFindBestPathFromCandidatePoints(
        Vector3[] candidatePoints,
        EnemyMovement movement,
        float navMeshSampleRange,
        float maxAcceptedSnapDistance,
        out PathChoice bestPath)
    {
        bestPath = new PathChoice();

        if (candidatePoints == null || candidatePoints.Length == 0 || movement == null)
        {
            return false;
        }

        bool foundPath = false;
        // 路径优先级：完整路径 > 有明确可破坏障碍的路径 > 未验证路径 > 环境阻挡路径。
        // 경로 우선순위: 완전 경로 > 파괴 가능한 장애물이 확인된 경로 > 미확인 경로 > 환경 장애물 경로.
        int bestPriority = int.MinValue;
        // 先设置成无限大，这样第一条有效路径一定可以成为暂时最优。
        // 처음에는 무한대로 설정해서 첫 번째 유효 경로가 임시 최적 경로가 될 수 있게 함.
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < candidatePoints.Length; i++)
        {
            PathChoice currentPath;

            // 对当前候选点进行一次完整问路，失败就跳过这个点。
            // 현재 후보 지점에 대해 한 번 길찾기 보고서를 만들고, 실패하면 이 지점은 건너뜀.
            if (!TryBuildPathChoice(
                candidatePoints[i],
                movement,
                navMeshSampleRange,
                maxAcceptedSnapDistance,
                out currentPath))
            {
                continue;
            }

            // 每条不完整路径在参与比较前，先验证路径断口正前方的第一个真实障碍。
            // 각 불완전 경로를 비교하기 전에 경로 단절 지점 바로 앞의 첫 실제 장애물을 검증한다.
            ValidatePhysicalBlocker(ref currentPath, movement);

            int currentPriority = GetPathPriority(currentPath);

            // 优先级高的路线先选；优先级相同时，再选 score 更小的路线。
            // 우선순위가 높은 경로를 먼저 선택하고, 우선순위가 같으면 score가 더 낮은 경로를 선택한다.
            bool shouldReplaceBestPath =
                !foundPath ||
                currentPriority > bestPriority ||
                (currentPriority == bestPriority && currentPath.score < bestScore);

            if (shouldReplaceBestPath)
            {
                bestPriority = currentPriority;
                bestScore = currentPath.score;
                bestPath = currentPath;
                foundPath = true;
            }
        }

        return foundPath;
    }

    // 尝试从目标身上的 TargetPathPoints 读取手动候选点。
    // 목표의 TargetPathPoints에서 수동 후보 지점을 읽어 온다.
    bool TryGetManualPathCandidatePoints(GameObject target, out Vector3[] candidatePoints)
    {
        candidatePoints = new Vector3[0];

        if (target == null)
        {
            return false;
        }

        TargetPathPoints targetPathPoints = target.GetComponent<TargetPathPoints>();

        if (targetPathPoints == null)
        {
            return false;
        }

        return targetPathPoints.TryGetWorldPoints(out candidatePoints);
    }

    // 对单个 destination 进行一次完整问路，并把结果打包成 PathChoice。
    // 하나의 destination에 대해 한 번 전체 경로 계산을 하고 결과를 PathChoice로 묶음.
    public bool TryBuildPathChoice(
        Vector3 destination,
        EnemyMovement movement,
        float navMeshSampleRange,
        out PathChoice pathChoice)
    {
        // 通用调用不限制 SamplePosition 修正距离，保持原有行为。
        // 일반 호출은 SamplePosition 보정 거리를 제한하지 않아 기존 동작을 유지한다.
        return TryBuildPathChoice(
            destination,
            movement,
            navMeshSampleRange,
            Mathf.Infinity,
            out pathChoice
        );
    }

    // 对单个目标点寻路，并限制 SamplePosition 可以把原始点修正多远。
    // 하나의 목표 지점으로 경로를 계산하고 SamplePosition이 원래 지점을 얼마나 멀리 보정할 수 있는지 제한한다.
    bool TryBuildPathChoice(
        Vector3 destination,
        EnemyMovement movement,
        float navMeshSampleRange,
        float maxAcceptedSnapDistance,
        out PathChoice pathChoice)
    {
        // out 参数先赋默认值，避免失败时外部拿到未赋值数据。
        // out 파라미터에 먼저 기본값을 넣어 실패해도 외부에서 미할당 데이터를 받지 않게 함.
        pathChoice = new PathChoice();

        if (movement == null)
        {
            return false;
        }

        NavMeshPath path;
        Vector3 lastReachablePoint;
        Vector3 navMeshDestination;

        // TryGetPath 内部会先 SamplePosition，再 CalculatePath。
        // TryGetPath 내부에서 먼저 SamplePosition을 하고, 그 다음 CalculatePath를 실행.
        if (!movement.TryGetPath(destination, navMeshSampleRange, out path, out lastReachablePoint, out navMeshDestination))
        {
            return false;
        }

        // 只比较水平偏移，避免地面高度的小误差把正常候选点误判为修正过远。
        // 지면 높이의 작은 오차로 정상 후보가 잘못 판정되지 않도록 수평 보정 거리만 비교한다.
        Vector3 snapOffset = navMeshDestination - destination;
        snapOffset.y = 0f;
        float destinationSnapDistance = snapOffset.magnitude;
        bool destinationWasSnappedTooFar = destinationSnapDistance > maxAcceptedSnapDistance;

        // 计算从敌人当前位置到路径最后点的实际路径长度。
        // 적 현재 위치에서 경로 마지막 지점까지의 실제 경로 길이를 계산.
        float pathLength = movement.GetPathLength(path);

        // 如果 corners 不足导致算出来是 0，就用当前位置到最后可到达点的直线距离兜底。
        // corners가 부족해서 0이 나오면 현재 위치에서 마지막 도달 가능 지점까지의 직선 거리로 보정.
        if (pathLength <= 0f)
        {
            pathLength = Vector3.Distance(transform.position, lastReachablePoint);
        }

        // 修正距离正常时，剩余距离计算到 NavMesh 点；修正过远时，必须计算到原始候选点。
        // 보정 거리가 정상이면 NavMesh 지점까지 계산하고, 너무 멀리 보정되었으면 원래 후보 지점까지 계산한다.
        Vector3 remainingDistanceTarget = destinationWasSnappedTooFar ? destination : navMeshDestination;
        float remainingDistance = Vector3.Distance(lastReachablePoint, remainingDistanceTarget);

        // 优先使用路径最后两个拐点计算真正的接近方向。
        // 경로의 마지막 두 코너를 사용해서 실제 접근 방향을 우선 계산한다.
        Vector3 approachDirection = Vector3.zero;
        Vector3[] corners = path.corners;

        if (!destinationWasSnappedTooFar && corners != null && corners.Length >= 2)
        {
            approachDirection = corners[corners.Length - 1] - corners[corners.Length - 2];
        }

        // 候选点被修正得太远时，必须朝原始候选点验证；普通情况下才使用修正后的 NavMesh 点兜底。
        // 후보 지점이 너무 멀리 보정되었으면 원래 후보 쪽을 검증하고, 일반적인 경우에만 보정된 NavMesh 지점을 예비값으로 사용한다.
        approachDirection.y = 0f;

        if (approachDirection.sqrMagnitude < 0.001f)
        {
            Vector3 directionTarget = destinationWasSnappedTooFar ? destination : navMeshDestination;
            approachDirection = directionTarget - lastReachablePoint;
            approachDirection.y = 0f;
        }

        if (approachDirection.sqrMagnitude >= 0.001f)
        {
            approachDirection.Normalize();
        }

        pathChoice.hasPath = true;
        // 即使修正后的点可以完整到达，只要它离手动原始点太远，就不能把路线当成真正完整。
        // 보정된 지점까지 완전히 도달할 수 있어도 수동 원래 지점에서 너무 멀면 실제 완전 경로로 보지 않는다.
        pathChoice.isComplete =
            path.status == NavMeshPathStatus.PathComplete &&
            !destinationWasSnappedTooFar;
        pathChoice.destination = navMeshDestination;
        pathChoice.requestedDestination = destination;
        pathChoice.destinationSnapDistance = destinationSnapDistance;
        pathChoice.destinationWasSnappedTooFar = destinationWasSnappedTooFar;
        pathChoice.lastReachablePoint = lastReachablePoint;
        pathChoice.pathLength = pathLength;
        pathChoice.approachDirection = approachDirection;
        pathChoice.blockerValidation = BlockerValidationResult.NotChecked;
        pathChoice.blockingBuilding = null;
        pathChoice.blockerHitPoint = lastReachablePoint;
        // 完整路径直接用路径长度；不完整路径额外加 remainingDistance，避免“很快被堵住的短路径”分数过低。
        // 완성 경로는 경로 길이만 사용. 불완전 경로는 remainingDistance를 더해서 너무 빨리 막힌 짧은 경로가 과하게 유리하지 않게 함.
        pathChoice.score = pathChoice.isComplete ? pathLength : pathLength + remainingDistance;

        return true;
    }

    // 给路径分配比较优先级。数值越大，越应该被选中。
    // 경로 비교 우선순위를 부여한다. 값이 클수록 먼저 선택해야 한다.
    int GetPathPriority(PathChoice pathChoice)
    {
        if (pathChoice.isComplete)
        {
            return 3;
        }

        if (pathChoice.blockerValidation == BlockerValidationResult.BreakableBuilding)
        {
            return 2;
        }

        if (pathChoice.blockerValidation == BlockerValidationResult.UnbreakableObstacle)
        {
            return 0;
        }

        return 1;
    }

    // 从不完整路径的最后可达点稍微后退，再沿路径最后一段向前做有宽度的物理检测。
    // 불완전 경로의 마지막 도달 가능 지점에서 조금 뒤로 물러난 뒤, 마지막 경로 방향으로 두께가 있는 물리 검사를 한다.
    void ValidatePhysicalBlocker(ref PathChoice pathChoice, EnemyMovement movement)
    {
        if (pathChoice.isComplete)
        {
            pathChoice.blockerValidation = BlockerValidationResult.NotChecked;
            return;
        }

        if (movement == null || pathBlockerLayer.value == 0)
        {
            pathChoice.blockerValidation = BlockerValidationResult.NotChecked;
            return;
        }

        Vector3 direction = pathChoice.approachDirection;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            pathChoice.blockerValidation = BlockerValidationResult.NoPhysicalBlocker;
            return;
        }

        direction.Normalize();

        float radius = Mathf.Max(0.02f, pathBlockerCastRadius);
        float backDistance = Mathf.Max(0f, pathBlockerCastBackDistance);
        float forwardDistance = Mathf.Max(0.1f, pathBlockerCastForwardDistance);
        float agentHeight = movement.Agent == null ? 1f : movement.Agent.height;

        // NavMesh 路径点通常在脚底高度，所以把检测中心抬到敌人身体中间。
        // NavMesh 경로 지점은 보통 발밑 높이이므로 검사 중심을 적 몸의 중간 높이로 올린다.
        Vector3 castCenter = pathChoice.lastReachablePoint + Vector3.up * Mathf.Max(radius, agentHeight * 0.5f);
        Vector3 castOrigin = castCenter - direction * backDistance;
        float castDistance = backDistance + forwardDistance;

        RaycastHit hit;
        bool didHit = Physics.SphereCast(
            castOrigin,
            radius,
            direction,
            out hit,
            castDistance,
            pathBlockerLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!didHit)
        {
            pathChoice.blockerValidation = BlockerValidationResult.NoPhysicalBlocker;
            DrawPathBlockerDebug(castOrigin, direction, castDistance, false, castOrigin + direction * castDistance);
            return;
        }

        pathChoice.blockerHitPoint = hit.point;
        DamageableBuilding building = hit.collider.GetComponentInParent<DamageableBuilding>();

        if (building != null && building.hp > 0f)
        {
            pathChoice.blockerValidation = BlockerValidationResult.BreakableBuilding;
            pathChoice.blockingBuilding = building;
            DrawPathBlockerDebug(castOrigin, direction, castDistance, true, hit.point);
            return;
        }

        // 第一个命中物没有 DamageableBuilding，说明这条路线先撞到不可破坏的环境障碍。
        // 첫 충돌 대상에 DamageableBuilding이 없으면 이 경로가 먼저 파괴 불가능한 환경 장애물에 막힌 것이다.
        pathChoice.blockerValidation = BlockerValidationResult.UnbreakableObstacle;
        DrawPathBlockerDebug(castOrigin, direction, castDistance, false, hit.point);
    }

    // Scene 视图调试线：绿色表示命中可破坏建筑，红色表示没找到可破坏障碍。
    // Scene 뷰 디버그 선: 초록색은 파괴 가능한 건물 명중, 빨간색은 파괴 가능한 장애물을 찾지 못함.
    void DrawPathBlockerDebug(
        Vector3 origin,
        Vector3 direction,
        float distance,
        bool foundBreakableBuilding,
        Vector3 hitPoint)
    {
        if (!showPathBlockerDebug)
        {
            return;
        }

        Color color = foundBreakableBuilding ? Color.green : Color.red;
        Debug.DrawLine(origin, origin + direction * distance, color, 0.2f);
        Debug.DrawRay(hitPoint, Vector3.up * 0.5f, color, 0.2f);
    }

    // 没有手动设置时，自动使用常见的环境墙和玩家建筑层。
    // 수동 설정이 없으면 일반적인 환경 벽과 플레이어 건물 레이어를 자동 사용한다.
    void SetupDefaultPathBlockerLayer()
    {
        if (pathBlockerLayer.value != 0)
        {
            return;
        }

        pathBlockerLayer = LayerMask.GetMask(
            "Wall",
            "EnvironmentWall",
            "PlayerWall",
            "PlayerTower"
        );
    }

    // 当路径不完整时，根据最后可到达点寻找可能堵住路线的玩家建筑。
    // 경로가 완성되지 않았을 때 마지막 도달 가능 지점을 기준으로 길을 막는 플레이어 건물을 찾음.
    public DamageableBuilding FindBuildingBlockingPath(
        Vector3 blockedPoint,
        Vector3 destination,
        LayerMask buildingLayer,
        float blockedPointSearchRange,
        float blockedPathForwardSearchRange)
    {
        // 记录目前最像“真正堵路建筑”的候选对象。
        // 현재까지 가장 실제로 길을 막고 있다고 판단되는 후보 건물을 저장.
        DamageableBuilding bestBuilding = null;
        float bestForwardDistance = Mathf.Infinity;

        // 搜索方向：最后可到达点 -> 原始目标点。
        // 검색 방향: 마지막 도달 가능 지점 -> 원래 목표 지점.
        Vector3 searchDirection = destination - blockedPoint; //搜索的方向(最后可到达点 -> 原始目标点)
        searchDirection.y = 0f; //去Y

        if (searchDirection.sqrMagnitude < 0.001f)
        {
            return null; //几乎重合
        }

        searchDirection.Normalize(); //只拿方向，不要长度

        // 先检查最后可达点附近的所有建筑，但不直接返回最近的。
        // 먼저 마지막 도달 가능 지점 근처의 모든 건물을 검사하되, 단순히 가장 가까운 건물을 바로 반환하지 않는다.
        Collider[] nearbyBuildings = Physics.OverlapSphere(blockedPoint, blockedPointSearchRange, buildingLayer);
        TryChooseBlockingBuildingFromColliders(
            nearbyBuildings,
            blockedPoint,
            searchDirection,
            blockedPointSearchRange,
            ref bestBuilding,
            ref bestForwardDistance
        );

        // 每隔一段距离做一次 OverlapSphere 搜索。
        // 일정 간격마다 OverlapSphere로 검색한다.
        float searchStep = Mathf.Max(1f, blockedPointSearchRange * 0.5f);

        for (float distance = searchStep; distance <= blockedPathForwardSearchRange; distance += searchStep)
        {   
            // 从最后可到达点 blockedPoint 出发，沿搜索方向，每隔一段距离得到一个搜索圆心。
            // 마지막 도달 가능 지점 blockedPoint에서 시작해 검색 방향으로 일정 거리마다 검색 중심점을 만든다.
            Vector3 searchCenter = blockedPoint + searchDirection * distance;
            Collider[] buildings = Physics.OverlapSphere(searchCenter, blockedPointSearchRange, buildingLayer);
            // 当前搜索圆里可能同时有墙和墙后的塔，所以要遍历全部建筑，而不是只取最近的一个。
            // 현재 검색 원 안에 벽과 벽 뒤의 타워가 동시에 있을 수 있으므로, 가장 가까운 하나만 고르지 않고 전부 검사한다.
            TryChooseBlockingBuildingFromColliders(
                buildings,
                blockedPoint,
                searchDirection,
                blockedPointSearchRange,
                ref bestBuilding,
                ref bestForwardDistance
            );
        }

        // 返回路径方向上最先挡住路线的建筑。
        // 경로 방향 기준으로 가장 먼저 길을 막는 건물을 반환.
        return bestBuilding;
    }

    // 从一组 Collider 中筛选真正挡在路径走廊里的建筑。
    // Collider 목록에서 실제 경로 통로 안을 막고 있는 건물을 고른다.
    void TryChooseBlockingBuildingFromColliders(
        Collider[] colliders,
        Vector3 blockedPoint,
        Vector3 searchDirection,
        float pathCorridorRadius,
        ref DamageableBuilding bestBuilding,
        ref float bestForwardDistance)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            // 检测到的 Collider 可能在建筑子物体上，所以向父级找 DamageableBuilding。
            // 감지된 Collider가 건물 자식 오브젝트에 있을 수 있으므로 부모에서 DamageableBuilding을 찾는다.
            DamageableBuilding candidate = colliders[i].GetComponentInParent<DamageableBuilding>();

            if (candidate == null)
            {
                continue;
            }

            // 找到这个建筑表面上离最后可达点最近的点。
            // 이 건물 표면에서 마지막 도달 가능 지점과 가장 가까운 점을 찾는다.
            Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(blockedPoint, candidate.gameObject);
            Vector3 toBuilding = closestPoint - blockedPoint;
            toBuilding.y = 0f;

            if (toBuilding.sqrMagnitude < 0.001f)
            {
                continue;
            }

            // forwardDistance 表示建筑在“去目标方向”上离 blockedPoint 多远。
            // forwardDistance는 건물이 목표 방향 기준으로 blockedPoint에서 얼마나 앞에 있는지를 뜻한다.
            float forwardDistance = Vector3.Dot(searchDirection, toBuilding);

            // 小于 0 说明建筑在 blockedPoint 后面，不是前方堵路建筑。
            // 0보다 작으면 건물이 blockedPoint 뒤쪽에 있으므로 앞을 막는 건물이 아니다.
            if (forwardDistance < 0f)
            {
                continue;
            }

            // 把建筑投影到路径中心线上，得到它在路径线上的对应点。
            // 건물을 경로 중심선에 투영해서 경로 선 위의 대응 지점을 구한다.
            Vector3 pointOnPathLine = blockedPoint + searchDirection * forwardDistance;

            // sideDistance 表示建筑离路径中心线有多远。
            // sideDistance는 건물이 경로 중심선에서 옆으로 얼마나 떨어져 있는지를 뜻한다.
            float sideDistance = Vector3.Distance(closestPoint, pointOnPathLine);

            // 离路径线太远，就认为不是这条路上的堵路建筑。
            // 경로 선에서 너무 멀면 이 경로를 막는 건물이 아니라고 본다.
            if (sideDistance > pathCorridorRadius)
            {
                continue;
            }

            // 谁在路径方向上更靠前，谁就是更应该先攻击的堵路建筑。
            // 경로 방향 기준으로 더 앞에 있는 건물이 먼저 공격해야 할 막는 건물이다.
            if (forwardDistance < bestForwardDistance)
            {
                bestForwardDistance = forwardDistance;
                bestBuilding = candidate;
            }
        }
    }

    // 通用工具：从一组 Collider 里找离指定点最近的 DamageableBuilding。
    // 공용 도구: Collider 목록에서 지정 지점과 가장 가까운 DamageableBuilding을 찾는다.
    public DamageableBuilding FindNearestBuildingFromColliders(Collider[] colliders, Vector3 distanceCenter)
    {
        if (colliders == null)
        {
            return null;
        }

        DamageableBuilding nearestBuilding = null;
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            // 检测到的 Collider 可能在建筑子物体上，所以向父级查找 DamageableBuilding。
            // 감지된 Collider가 건물 자식 오브젝트에 있을 수 있으므로 부모에서 DamageableBuilding을 찾음.
            DamageableBuilding building = colliders[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            // 用目标表面最近距离，而不是只用 transform.position 的直线距离。
            // transform.position 직선 거리가 아니라 목표 표면 기준의 가장 가까운 거리 사용.
            float distance = EnemyTargetUtility.GetDistanceToTarget(distanceCenter, building.gameObject);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestBuilding = building;
            }
        }

        return nearestBuilding; //最近的建筑返还
    }

    // 这个函数只负责生成“寻路候选点”。
    // 它不判断路径能不能走，也不保证这个点能攻击目标。
    // 后面 TryBuildPathChoice 会对这些点算路径，TryFindBestPathToTarget 会选最合适的一条。
    // 이 함수는 "경로 후보 지점"만 생성함.
    // 경로가 실제로 가능한지 판단하지 않고, 이 지점에서 공격할 수 있다고 보장하지도 않음.
    // 이후 TryBuildPathChoice가 이 점들에 대해 경로를 계산하고, TryFindBestPathToTarget이 가장 적절한 경로를 선택함.
    Vector3[] GetAutomaticPathCandidatePoints(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float targetPathPointExtraDistance)
    {
        float enemyAttackRange = enemyAI == null || enemyAI.AttackModule == null ? 0f : enemyAI.AttackModule.attackRange;
        Vector3 targetCenter = target.transform.position; // 默认目标中心 / 기본 목표 중심
        float targetRadius = enemyAttackRange + targetPathPointExtraDistance; // 默认候选点半径 / 기본 후보 지점 반지름

        Bounds bounds; // 临时变量，用来保存目标整体范围 / 목표 전체 범위를 저장하는 임시 변수

        // 尝试通过目标的 Renderer / Collider 计算目标整体包围盒 Bounds
        // 목표의 Renderer / Collider를 통해 전체 Bounds를 계산해 봄
        if (EnemyTargetUtility.TryGetTargetBounds(target, out bounds))
        {
            targetCenter = bounds.center; // 实际中心点 / 실제 중심점
            float horizontalSize = Mathf.Max(bounds.extents.x, bounds.extents.z); // 取水平最长边的半长 / 수평 방향에서 가장 긴 반쪽 길이
            // 候选点半径 = 目标中心到边缘的大概距离 + 敌人需要站在外面的距离 + 额外容错距离
            // 후보 지점 반지름 = 목표 중심에서 가장자리까지의 대략적인 거리 + 적이 바깥에 서기 위한 거리 + 추가 여유 거리
            targetRadius = horizontalSize + Mathf.Max(enemyAttackRange * 0.8f, movement.Radius + 0.3f) + targetPathPointExtraDistance;
        }

        Vector3 enemyDirection = transform.position - targetCenter; // 目标中心 -> 敌人位置的方向 / 목표 중심 -> 적 위치 방향
        enemyDirection.y = 0f; // 去掉 Y，只看地面平面方向 / Y를 제거하고 지면 평면 방향만 사용

        if (enemyDirection.sqrMagnitude < 0.001f)
        {
            enemyDirection = -transform.forward; // 太近或重合时使用敌人后方作为兜底方向 / 너무 가깝거나 겹치면 적의 뒤쪽 방향을 기본값으로 사용
        }

        enemyDirection.Normalize(); // 去掉长度，只保留方向 / 길이를 제거하고 방향만 사용

        //用世界上方向 Vector3.up 和 enemyDirection 做叉乘，算出一个水平侧方向
        // 根据“目标到敌人方向”算出一个水平侧边方向
        // Vector3.up과 enemyDirection의 외적으로 수평 측면 방향을 계산
        Vector3 rightDirection = Vector3.Cross(Vector3.up, enemyDirection).normalized;
        Vector3 leftDiagonal = (enemyDirection + rightDirection).normalized; // 斜向候选方向之一 / 대각선 후보 방향 중 하나
        Vector3 rightDiagonal = (enemyDirection - rightDirection).normalized;// 斜向候选方向之一 / 대각선 후보 방향 중 하나

        //得到目标(Core)碰撞体/边界上离敌人当前位置最近的点
        // 목표(Core)의 Collider/경계에서 적 현재 위치와 가장 가까운 점을 얻음
        Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, target);
        Vector3 closestDirection = transform.position - closestPoint; // 表面最近点 -> 敌人的方向 / 표면의 가장 가까운 점 -> 적 방향
        closestDirection.y = 0f; // 不考虑 Y / Y는 고려하지 않음

        if (closestDirection.sqrMagnitude < 0.001f)
        {
            closestDirection = enemyDirection; // 离得太近就使用默认方向 / 너무 가까우면 기본 방향 사용
        }

        closestDirection.Normalize(); // 去掉长度，只拿方向 / 길이를 제거하고 방향만 사용

        //寻路候选点 不是真正的攻击位置
        // 경로 후보 지점이며 실제 공격 위치가 아님
        //                       ○
        //               ○              ○

        // 敌人(적) →  ○      [  Core  ]    ○

        //               ○              ○
        //                       ○
        Vector3[] candidatePoints =
        {
            // 目标表面最近点往敌人方向外推一点，通常是最自然的接近点。
            // 목표 표면의 가장 가까운 점을 적 방향으로 조금 밀어낸 지점. 보통 가장 자연스러운 접근 지점.
            closestPoint + closestDirection * Mathf.Max(enemyAttackRange * 0.8f, movement.Radius + 0.3f),
            targetCenter + enemyDirection * targetRadius, // 目标靠近敌人方向的外圈点 / 목표에서 적 쪽 외곽 지점
            targetCenter - enemyDirection * targetRadius, // 目标远离敌人方向的外圈点 / 목표에서 적 반대쪽 외곽 지점
            targetCenter + rightDirection * targetRadius, // 侧边方向候选点 / 측면 방향 후보 지점
            targetCenter - rightDirection * targetRadius,// 侧边反方向候选点 / 측면 반대 방향 후보 지점
            targetCenter + leftDiagonal * targetRadius, // 斜方向候选点 / 대각선 방향 후보 지점
            targetCenter - leftDiagonal * targetRadius,// 斜方向反向候选点 / 대각선 반대 방향 후보 지점
            targetCenter + rightDiagonal * targetRadius, // 另一条斜方向候选点 / 다른 대각선 방향 후보 지점
            targetCenter - rightDiagonal * targetRadius,// 另一条斜方向反向候选点 / 다른 대각선 반대 방향 후보 지점
            targetCenter // 中心点兜底 / 중심점 예비 후보
        };

        return candidatePoints; // 返回这些候选点 / 후보 지점들을 반환
    }

    // 在某个点附近搜索最近的可破坏建筑。
    // 특정 지점 주변에서 가장 가까운 파괴 가능한 건물을 검색.
    DamageableBuilding FindBuildingNearPoint(Vector3 center, LayerMask buildingLayer, float blockedPointSearchRange)
    {
        Collider[] buildings = Physics.OverlapSphere(center, blockedPointSearchRange, buildingLayer);
        return FindNearestBuildingFromColliders(buildings, center);
    }
}
