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
                // 远程型不要求目标建筑的 NavMesh 路径完整。
                // 因为即使走不到塔旁边，只要塔已经进入射程并且攻击线没有被挡住，远程型仍然可以攻击。
                // 원거리형은 목표 건물까지의 NavMesh 경로가 완전할 필요가 없다.
                // 타워 옆까지 걸어갈 수 없어도 사거리 안에 있고 공격선이 막히지 않았다면 공격할 수 있기 때문이다.
                GameObject tower = FindNearestBuildingByDistance(towerLayer, detectRange);

                if (tower != null)
                {
                    return tower;
                }

                // 感知范围里没有塔时，继续保留原来的“墙作为第二优先目标”规则。
                // 감지 범위 안에 타워가 없으면 기존 규칙대로 벽을 두 번째 우선 타깃으로 사용한다.
                return FindNearestBuildingByDistance(wallLayer, detectRange);
        }

        // Standard 类型没有额外优先目标，默认继续朝 Core 走。
        // Standard 타입은 별도 우선 타깃이 없으므로 기본적으로 Core로 이동한다.
        return null;
    }

    // 不要求路径完整，只按敌人到建筑表面的实际距离选择最近建筑。
    // 경로 완전 여부를 요구하지 않고 적에서 건물 표면까지의 실제 거리로 가장 가까운 건물을 선택한다.
    GameObject FindNearestBuildingByDistance(LayerMask layer, float detectRange)
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, detectRange, layer);
        DamageableBuilding building = FindNearestBuildingFromColliders(targets, transform.position);

        if (building == null || building.hp <= 0f)
        {
            return null;
        }

        return building.gameObject;
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

    // 计算敌人走到某个建筑攻击位置所需的真实 NavMesh 路径长度。
    // 적이 특정 건물의 공격 위치까지 이동하는 실제 NavMesh 경로 길이를 계산한다.
    // 返回 true 代表至少找到了一个可以完整走到的攻击位置，pathLength 是其中路线最短的长度。
    // true를 반환하면 완전히 도달 가능한 공격 위치를 찾았다는 뜻이며, pathLength는 그중 가장 짧은 경로 길이다.
    public bool TryGetReachableBuildingPathLength(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        BuildingAttackSlotManager attackSlotManager,
        float buildingMovePointSampleRange,
        out float pathLength)
    {
        pathLength = Mathf.Infinity;

        if (target == null || enemyAI == null || movement == null || attackSlotManager == null)
        {
            return false;
        }

        DamageableBuilding building = target.GetComponentInParent<DamageableBuilding>();

        if (building == null || building.hp <= 0f)
        {
            return false;
        }

        Vector3 movePoint;

        // 先找建筑周围能够站过去攻击的位置。
        // 먼저 건물 주변에서 실제로 이동할 수 있는 공격 위치를 찾는다.
        if (!attackSlotManager.TryGetReachableMovePointNearTarget(
            building.gameObject,
            enemyAI,
            movement,
            buildingMovePointSampleRange,
            out movePoint))
        {
            return false;
        }

        NavMeshPath path;
        Vector3 lastReachablePoint;

        // 再计算敌人当前位置到这个攻击位置的路径。
        // 그다음 적의 현재 위치에서 공격 위치까지의 경로를 계산한다.
        if (!movement.TryGetPath(
            movePoint,
            buildingMovePointSampleRange,
            out path,
            out lastReachablePoint))
        {
            return false;
        }

        // 路径不完整，说明敌人目前不能真正走到这个建筑的攻击位置。
        // 경로가 완전하지 않으면 현재 이 건물의 공격 위치까지 실제로 갈 수 없다.
        if (path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        pathLength = movement.GetPathLength(path);

        // 极少数情况下路径拐点不足，使用直线距离作为兜底分数。
        // 드물게 경로 코너가 부족하면 직선거리를 대체 점수로 사용한다.
        if (pathLength <= 0f)
        {
            pathLength = Vector3.Distance(transform.position, movePoint);
        }

        return true;
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

            if (building == null || building.hp <= 0f)
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

            float pathLength;

            // 使用统一的可达性和路径长度计算，避免“第一次选目标”和“之后比较目标”采用不同标准。
            // 동일한 도달 가능 여부와 경로 길이 계산을 사용해서 최초 선택과 이후 비교 기준이 달라지지 않게 한다.
            if (!TryGetReachableBuildingPathLength(
                building.gameObject,
                enemyAI,
                movement,
                attackSlotManager,
                buildingMovePointSampleRange,
                out pathLength))
            {
                continue;
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
