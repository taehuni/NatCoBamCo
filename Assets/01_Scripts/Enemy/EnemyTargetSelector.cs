using UnityEngine;
using UnityEngine.AI;

// 敌人优先目标选择模块：根据敌人种类，在感知范围内选择更想攻击的目标。
// 적 우선 타깃 선택 모듈: 적 종류에 따라 감지 범위 안에서 우선 공격할 타깃을 고른다.
// 它只负责“选谁”，不负责移动、攻击、堵路判断。
// 이 모듈은 "누구를 고를지"만 담당한다. 이동, 공격, 경로 막힘 판단은 담당하지 않는다.
public class EnemyTargetSelector : MonoBehaviour
{
    [Header("Target Search Data / 타깃 탐색 데이터")]
    public float detectRange;
    public float loseTargetRange;

    public GameObject FindPriorityTarget(
        EnemyAI enemyAI,
        EnemyMovement movement,
        BuildingAttackSlotManager attackSlotManager,
        LayerMask playerLayer,
        LayerMask wallLayer,
        LayerMask towerLayer,
        float detectRange,
        float buildingMovePointSampleRange)
    {
        if (enemyAI == null)
        {
            return null;
        }

        // 根据敌人种类决定优先目标。
        // 적 종류에 따라 우선 타깃을 결정한다.
        EnemyAI.EnemyClass enemyClass = enemyAI.ClassTraits == null ? EnemyAI.EnemyClass.Standard : enemyAI.ClassTraits.enemyClass;

        switch (enemyClass)
        {
            case EnemyAI.EnemyClass.Fast:
                // 高速型优先找玩家。
                // Fast 타입은 플레이어를 우선 찾는다.
                return FindPlayerTarget(playerLayer, detectRange);

            case EnemyAI.EnemyClass.Tank:
                // 坦克型优先找墙。
                // Tank 타입은 벽을 우선 찾는다.
                return FindNearestBuildingByLayer(wallLayer, detectRange, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);

            case EnemyAI.EnemyClass.Ranged:
                // 远程型优先找塔，找不到塔再找墙。
                // Ranged 타입은 타워를 먼저 찾고, 없으면 벽을 찾는다.
                GameObject tower = FindNearestBuildingByLayer(towerLayer, detectRange, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);

                if (tower != null)
                {
                    return tower;
                }

                return FindNearestBuildingByLayer(wallLayer, detectRange, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);
        }

        // Standard 类型没有额外优先目标，默认继续朝 Core 走。
        // Standard 타입은 별도 우선 타깃이 없으므로 기본적으로 Core로 이동한다.
        return null;
    }

    public GameObject FindPlayerTarget(LayerMask playerLayer, float detectRange)
    {
        // 在感知范围内查找 PlayerLayer 上的碰撞体。
        // 감지 범위 안에서 PlayerLayer에 있는 콜라이더를 찾는다.
        Collider[] targets = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

            // 碰撞体可能在玩家子物体上，所以向父级找 PlayerController。
            // 콜라이더가 플레이어 자식 오브젝트에 있을 수 있으므로 부모 쪽에서 PlayerController를 찾는다.
            PlayerController player = targets[i].GetComponentInParent<PlayerController>();

            if (player != null)
            {
                return player.gameObject;
            }
        }

        return null;
    }

    public GameObject FindNearestBuildingByLayer(
        LayerMask layer,
        float detectRange,
        EnemyAI enemyAI,
        EnemyMovement movement,
        BuildingAttackSlotManager attackSlotManager,
        float buildingMovePointSampleRange)
    {
        // 先用图层在范围内找候选建筑。
        // 먼저 레이어와 범위로 후보 건물을 찾는다.
        Collider[] targets = Physics.OverlapSphere(transform.position, detectRange, layer);
        // 优先选择“敌人能沿 NavMesh 最短路径走到攻击点”的建筑。
        // 우선 NavMesh 최단 경로로 공격 위치까지 갈 수 있는 건물을 선택한다.
        DamageableBuilding building = FindShortestPathBuildingFromColliders(targets, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);

        if (building == null)
        {
            // 如果没有可完整走到攻击点的建筑，就退一步选择距离最近的建筑。
            // 공격 위치까지 완전히 갈 수 있는 건물이 없으면, 한 단계 낮춰 가장 가까운 건물을 선택한다.
            building = FindNearestBuildingFromColliders(targets, transform.position);
        }

        if (building != null)
        {
            return building.gameObject;
        }

        return null;
    }

    public DamageableBuilding FindNearestBuildingFromColliders(Collider[] colliders, Vector3 distanceCenter)
    {
        // 从一组 Collider 中找离 distanceCenter 最近的 DamageableBuilding。
        // Collider 목록에서 distanceCenter와 가장 가까운 DamageableBuilding을 찾는다.
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

            // Collider 可能在建筑子物体上，所以向父级找 DamageableBuilding。
            // Collider가 건물의 자식 오브젝트에 있을 수 있으므로 부모에서 DamageableBuilding을 찾는다.
            DamageableBuilding building = colliders[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            // 用“到建筑表面最近点”的距离，不单纯用建筑中心点距离。
            // 건물 중심점이 아니라 "건물 표면까지의 가장 가까운 거리"를 사용한다.
            float distance = EnemyTargetUtility.GetDistanceToTarget(distanceCenter, building.gameObject);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestBuilding = building;
            }
        }

        return nearestBuilding;
    }

    DamageableBuilding FindShortestPathBuildingFromColliders(
        Collider[] colliders,
        EnemyAI enemyAI,
        EnemyMovement movement,
        BuildingAttackSlotManager attackSlotManager,
        float buildingMovePointSampleRange)
    {
        // 在候选建筑里找“完整可达路径最短”的建筑。
        // 후보 건물 중 "완전히 도달 가능한 경로가 가장 짧은" 건물을 찾는다.
        if (colliders == null)
        {
            return null;
        }

        DamageableBuilding bestBuilding = null;
        float shortestPathLength = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            // 每个 Collider 都向父级转成建筑对象。
            // 각 Collider를 부모 쪽의 건물 오브젝트로 변환한다.
            DamageableBuilding building = colliders[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            Vector3 movePoint;

            // 找这个建筑附近可以站过去攻击的位置。
            // 이 건물 근처에서 서서 공격할 수 있는 위치를 찾는다.
            if (!attackSlotManager.TryGetReachableMovePointNearTarget(
                building.gameObject,
                enemyAI,
                movement,
                buildingMovePointSampleRange,
                out movePoint))
            {
                continue;
            }

            NavMeshPath path;
            Vector3 lastReachablePoint;

            // 计算敌人到这个攻击位置的路径。
            // 적이 이 공격 위치까지 갈 수 있는 경로를 계산한다.
            if (!movement.TryGetPath(movePoint, buildingMovePointSampleRange, out path, out lastReachablePoint))
            {
                continue;
            }

            // 这里只接受完整路径，不完整路径说明敌人走不到这个攻击位置。
            // 여기서는 완전한 경로만 허용한다. 불완전한 경로는 공격 위치까지 갈 수 없다는 뜻이다.
            if (path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            // 计算真实路径长度，而不是直线距离。
            // 직선 거리가 아니라 실제 경로 길이를 계산한다.
            float pathLength = movement.GetPathLength(path);

            if (pathLength <= 0f)
            {
                // 极少数情况下路径拐点不足，就用直线距离兜底。
                // 드물게 경로 코너가 부족하면 직선 거리로 대체한다.
                pathLength = Vector3.Distance(transform.position, movePoint);
            }

            // 谁的路径更短，就选谁。
            // 경로가 더 짧은 건물을 선택한다.
            if (pathLength < shortestPathLength)
            {
                shortestPathLength = pathLength;
                bestBuilding = building;
            }
        }

        return bestBuilding;
    }
}
