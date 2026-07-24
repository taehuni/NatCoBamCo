using UnityEngine;
using UnityEngine.AI;

public class EnemyPathBlockHandler : MonoBehaviour
{
    public struct PathChoice
    {
        public bool hasPath;
        public bool isComplete;
        public Vector3 destination;
        public Vector3 lastReachablePoint;
        public float pathLength;
        public float score;
    }

    public bool IsPathComplete(
        Vector3 destination,
        EnemyMovement movement,
        float navMeshSampleRange,
        out PathChoice pathChoice)
    {
        if (!TryBuildPathChoice(destination, movement, navMeshSampleRange, out pathChoice))
        {
            return false;
        }

        return pathChoice.isComplete;
    }

    public bool TryFindBestPathToTarget(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float navMeshSampleRange,
        float targetPathPointExtraDistance,
        out PathChoice bestPath)
    {
        bestPath = new PathChoice();

        if (target == null || enemyAI == null || movement == null)
        {
            return false;
        }

        Vector3[] candidatePoints = GetPathCandidatePoints(target, enemyAI, movement, targetPathPointExtraDistance);

        bool foundPath = false;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < candidatePoints.Length; i++)
        {
            PathChoice currentPath;

            if (!TryBuildPathChoice(candidatePoints[i], movement, navMeshSampleRange, out currentPath))
            {
                continue;
            }

            if (currentPath.score < bestScore)
            {
                bestScore = currentPath.score;
                bestPath = currentPath;
                foundPath = true;
            }
        }

        return foundPath;
    }

    public bool TryBuildPathChoice(
        Vector3 destination,
        EnemyMovement movement,
        float navMeshSampleRange,
        out PathChoice pathChoice)
    {
        pathChoice = new PathChoice();

        if (movement == null)
        {
            return false;
        }

        NavMeshPath path;
        Vector3 lastReachablePoint;
        Vector3 navMeshDestination;

        if (!movement.TryGetPath(destination, navMeshSampleRange, out path, out lastReachablePoint, out navMeshDestination))
        {
            return false;
        }

        float pathLength = movement.GetPathLength(path);

        if (pathLength <= 0f)
        {
            pathLength = Vector3.Distance(transform.position, lastReachablePoint);
        }

        float remainingDistance = Vector3.Distance(lastReachablePoint, navMeshDestination);

        pathChoice.hasPath = true;
        pathChoice.isComplete = path.status == NavMeshPathStatus.PathComplete;
        pathChoice.destination = navMeshDestination;
        pathChoice.lastReachablePoint = lastReachablePoint;
        pathChoice.pathLength = pathLength;
        pathChoice.score = pathChoice.isComplete ? pathLength : pathLength + remainingDistance;

        return true;
    }

    public DamageableBuilding FindBuildingBlockingPath(
        Vector3 blockedPoint,
        Vector3 destination,
        LayerMask buildingLayer,
        float blockedPointSearchRange,
        float blockedPathForwardSearchRange)
    {
        DamageableBuilding building = FindBuildingNearPoint(blockedPoint, buildingLayer, blockedPointSearchRange);

        if (building != null)
        {
            return building;
        }

        Vector3 searchDirection = destination - blockedPoint;
        searchDirection.y = 0f;

        if (searchDirection.sqrMagnitude < 0.001f)
        {
            return null;
        }

        searchDirection.Normalize();

        float searchStep = Mathf.Max(1f, blockedPointSearchRange * 0.5f);
        DamageableBuilding bestBuilding = null;
        float bestDistance = Mathf.Infinity;

        for (float distance = searchStep; distance <= blockedPathForwardSearchRange; distance += searchStep)
        {
            Vector3 searchCenter = blockedPoint + searchDirection * distance;
            Collider[] buildings = Physics.OverlapSphere(searchCenter, blockedPointSearchRange, buildingLayer);
            DamageableBuilding candidate = FindNearestBuildingFromColliders(buildings, searchCenter);

            if (candidate == null)
            {
                continue;
            }

            Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(blockedPoint, candidate.gameObject);
            Vector3 toBuilding = closestPoint - blockedPoint;
            toBuilding.y = 0f;

            if (toBuilding.sqrMagnitude < 0.001f)
            {
                continue;
            }

            float directionDot = Vector3.Dot(searchDirection, toBuilding.normalized);

            if (directionDot < 0.2f)
            {
                continue;
            }

            float buildingDistance = toBuilding.magnitude;

            if (buildingDistance < bestDistance)
            {
                bestDistance = buildingDistance;
                bestBuilding = candidate;
            }
        }

        return bestBuilding;
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

    Vector3[] GetPathCandidatePoints(
        GameObject target,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float targetPathPointExtraDistance)
    {
        Vector3 targetCenter = target.transform.position;
        float targetRadius = enemyAI.attackRange + targetPathPointExtraDistance;

        Bounds bounds;

        if (EnemyTargetUtility.TryGetTargetBounds(target, out bounds))
        {
            targetCenter = bounds.center;
            float horizontalSize = Mathf.Max(bounds.extents.x, bounds.extents.z);
            targetRadius = horizontalSize + Mathf.Max(enemyAI.attackRange * 0.8f, movement.Radius + 0.3f) + targetPathPointExtraDistance;
        }

        Vector3 enemyDirection = transform.position - targetCenter;
        enemyDirection.y = 0f;

        if (enemyDirection.sqrMagnitude < 0.001f)
        {
            enemyDirection = -transform.forward;
        }

        enemyDirection.Normalize();

        Vector3 rightDirection = Vector3.Cross(Vector3.up, enemyDirection).normalized;
        Vector3 leftDiagonal = (enemyDirection + rightDirection).normalized;
        Vector3 rightDiagonal = (enemyDirection - rightDirection).normalized;

        Vector3 closestPoint = EnemyTargetUtility.GetClosestPointToTarget(transform.position, target);
        Vector3 closestDirection = transform.position - closestPoint;
        closestDirection.y = 0f;

        if (closestDirection.sqrMagnitude < 0.001f)
        {
            closestDirection = enemyDirection;
        }

        closestDirection.Normalize();

        Vector3[] candidatePoints =
        {
            closestPoint + closestDirection * Mathf.Max(enemyAI.attackRange * 0.8f, movement.Radius + 0.3f),
            targetCenter + enemyDirection * targetRadius,
            targetCenter - enemyDirection * targetRadius,
            targetCenter + rightDirection * targetRadius,
            targetCenter - rightDirection * targetRadius,
            targetCenter + leftDiagonal * targetRadius,
            targetCenter - leftDiagonal * targetRadius,
            targetCenter + rightDiagonal * targetRadius,
            targetCenter - rightDiagonal * targetRadius,
            targetCenter
        };

        return candidatePoints;
    }

    DamageableBuilding FindBuildingNearPoint(Vector3 center, LayerMask buildingLayer, float blockedPointSearchRange)
    {
        Collider[] buildings = Physics.OverlapSphere(center, blockedPointSearchRange, buildingLayer);
        return FindNearestBuildingFromColliders(buildings, center);
    }
}
