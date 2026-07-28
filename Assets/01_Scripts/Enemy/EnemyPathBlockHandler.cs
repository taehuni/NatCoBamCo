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
    [Header("Path Block Data / 경로 막힘 데이터")]
    public float blockedPathSearchRange;

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
        // 这条路径最后能走到的位置。
        // 이 경로에서 마지막으로 도달 가능한 위치.
        public Vector3 lastReachablePoint;
        // 从当前位置沿 NavMesh 走到 lastReachablePoint 的路径长度。
        // 현재 위치에서 NavMesh를 따라 lastReachablePoint까지 가는 경로 길이.
        public float pathLength;
        // 用来比较路线优劣的分数，越小越优先。
        // 경로 우선순위를 비교하기 위한 점수. 낮을수록 우선.
        public float score;
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

        // 目标是有体积的物体，所以先在目标周围生成多个寻路候选点。
        // 목표는 부피가 있는 오브젝트이므로 목표 주변에 여러 경로 후보 지점을 먼저 생성.
        Vector3[] candidatePoints = GetPathCandidatePoints(target, enemyAI, movement, targetPathPointExtraDistance);

        bool foundPath = false;
        // 先设置成无限大，这样第一条有效路径一定可以成为暂时最优。
        // 처음에는 무한대로 설정해서 첫 번째 유효 경로가 임시 최적 경로가 될 수 있게 함.
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < candidatePoints.Length; i++)
        {
            PathChoice currentPath;

            // 对当前候选点进行一次完整问路，失败就跳过这个点。
            // 현재 후보 지점에 대해 한 번 길찾기 보고서를 만들고, 실패하면 이 지점은 건너뜀.
            if (!TryBuildPathChoice(candidatePoints[i], movement, navMeshSampleRange, out currentPath))
            {
                continue;
            }

            // 谁的 score 更小，就把谁记录成当前最佳路径。
            // score가 더 낮은 경로를 현재 최적 경로로 기록.
            if (currentPath.score < bestScore)
            {
                bestScore = currentPath.score;
                bestPath = currentPath;
                foundPath = true;
            }
        }

        return foundPath;
    }

    // 对单个 destination 进行一次完整问路，并把结果打包成 PathChoice。
    // 하나의 destination에 대해 한 번 전체 경로 계산을 하고 결과를 PathChoice로 묶음.
    public bool TryBuildPathChoice(
        Vector3 destination,
        EnemyMovement movement,
        float navMeshSampleRange,
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

        // 计算从敌人当前位置到路径最后点的实际路径长度。
        // 적 현재 위치에서 경로 마지막 지점까지의 실제 경로 길이를 계산.
        float pathLength = movement.GetPathLength(path);

        // 如果 corners 不足导致算出来是 0，就用当前位置到最后可到达点的直线距离兜底。
        // corners가 부족해서 0이 나오면 현재 위치에서 마지막 도달 가능 지점까지의 직선 거리로 보정.
        if (pathLength <= 0f)
        {
            pathLength = Vector3.Distance(transform.position, lastReachablePoint);
        }

        // 剩余距离：最后可到达点到目标点之间还差多远。
        // 남은 거리: 마지막 도달 가능 지점에서 목표 지점까지 얼마나 남았는지.
        float remainingDistance = Vector3.Distance(lastReachablePoint, navMeshDestination);

        pathChoice.hasPath = true;
        pathChoice.isComplete = path.status == NavMeshPathStatus.PathComplete;
        pathChoice.destination = navMeshDestination;
        pathChoice.lastReachablePoint = lastReachablePoint;
        pathChoice.pathLength = pathLength;
        // 完整路径直接用路径长度；不完整路径额外加 remainingDistance，避免“很快被堵住的短路径”分数过低。
        // 완성 경로는 경로 길이만 사용. 불완전 경로는 remainingDistance를 더해서 너무 빨리 막힌 짧은 경로가 과하게 유리하지 않게 함.
        pathChoice.score = pathChoice.isComplete ? pathLength : pathLength + remainingDistance;

        return true;
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
        // 先直接在堵路点附近找建筑。
        // 먼저 막힌 지점 주변에서 건물을 찾음.
        DamageableBuilding building = FindBuildingNearPoint(blockedPoint, buildingLayer, blockedPointSearchRange);

        if (building != null)
        {
            return building;
        }

        // 如果堵路点附近没找到，就沿着“堵路点 -> 目标点”的方向继续往前搜索。
        // 막힌 지점 주변에서 못 찾으면 "막힌 지점 -> 목표 지점" 방향으로 더 앞쪽을 검색.
        Vector3 searchDirection = destination - blockedPoint; //搜索的方向(最后可到达点 -> 原始目标点)
        searchDirection.y = 0f; //去Y

        if (searchDirection.sqrMagnitude < 0.001f)
        {
            return null; //几乎重合
        }

        searchDirection.Normalize(); //只拿方向，不要长度

        // 每隔一段距离做一次 OverlapSphere 搜索。
        // 일정 간격마다 OverlapSphere로 검색.
        float searchStep = Mathf.Max(1f, blockedPointSearchRange * 0.5f);
        DamageableBuilding bestBuilding = null;
        float bestDistance = Mathf.Infinity;

        for (float distance = searchStep; distance <= blockedPathForwardSearchRange; distance += searchStep)
        {   
            //从最后可到达点 blockedPoint 出发，沿搜索方向，每隔一段距离得到一个搜索圆心
            Vector3 searchCenter = blockedPoint + searchDirection * distance;
            Collider[] buildings = Physics.OverlapSphere(searchCenter, blockedPointSearchRange, buildingLayer);
            //在当前搜索圆检测到的 Collider 里,找离 searchCenter 最近的 DamageableBuilding。
            DamageableBuilding candidate = FindNearestBuildingFromColliders(buildings, searchCenter);

            if (candidate == null)
            {
                continue;
            }

            //找candidate建筑表面上离最后可到达点最近的点
            Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(blockedPoint, candidate.gameObject);
            Vector3 toBuilding = closestPoint - blockedPoint; //得到方向
            toBuilding.y = 0f; //去掉Y

            if (toBuilding.sqrMagnitude < 0.001f)
            {
                continue; //方向太小就跳过，几乎重合的情况
            }

            // 点积用来判断建筑是不是大致在搜索方向前方，避免找到侧后方无关建筑。
            // 내적은 건물이 검색 방향 앞쪽에 있는지 판단하는 데 사용. 옆이나 뒤의 무관한 건물을 피함.
            float directionDot = Vector3.Dot(searchDirection, toBuilding.normalized);

            if (directionDot < 0.2f)
            {
                continue;
            }

            float buildingDistance = toBuilding.magnitude; //距离等于方向的长度

            if (buildingDistance < bestDistance)
            {
                bestDistance = buildingDistance; //最后会拿到最近的距离
                bestBuilding = candidate; //最近的建筑
            }
        }

        return bestBuilding; //返还这个最近的建筑
    }

    // 从一组 Collider 中找到离 distanceCenter 最近的 DamageableBuilding
    // Collider 배열에서 distanceCenter와 가장 가까운 DamageableBuilding을 찾음.
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
    Vector3[] GetPathCandidatePoints(
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
