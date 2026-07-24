using UnityEngine;
using UnityEngine.AI;

public class EnemyTargetSelector : MonoBehaviour
{
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

        switch (enemyAI.enemyClass)
        {
            case EnemyAI.EnemyClass.Fast:
                return FindPlayerTarget(playerLayer, detectRange);

            case EnemyAI.EnemyClass.Tank:
                return FindNearestBuildingByLayer(wallLayer, detectRange, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);

            case EnemyAI.EnemyClass.Ranged:
                GameObject tower = FindNearestBuildingByLayer(towerLayer, detectRange, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);

                if (tower != null)
                {
                    return tower;
                }

                return FindNearestBuildingByLayer(wallLayer, detectRange, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);
        }

        return null;
    }

    public GameObject FindPlayerTarget(LayerMask playerLayer, float detectRange)
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

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
        Collider[] targets = Physics.OverlapSphere(transform.position, detectRange, layer);
        DamageableBuilding building = FindShortestPathBuildingFromColliders(targets, enemyAI, movement, attackSlotManager, buildingMovePointSampleRange);

        if (building == null)
        {
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

            DamageableBuilding building = colliders[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

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

            DamageableBuilding building = colliders[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            Vector3 movePoint;

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

            if (!movement.TryGetPath(movePoint, buildingMovePointSampleRange, out path, out lastReachablePoint))
            {
                continue;
            }

            if (path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float pathLength = movement.GetPathLength(path);

            if (pathLength <= 0f)
            {
                pathLength = Vector3.Distance(transform.position, movePoint);
            }

            if (pathLength < shortestPathLength)
            {
                shortestPathLength = pathLength;
                bestBuilding = building;
            }
        }

        return bestBuilding;
    }
}
