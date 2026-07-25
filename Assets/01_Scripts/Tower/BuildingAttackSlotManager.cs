using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BuildingAttackSlotManager : MonoBehaviour
{
    private int currentAttackBuildingId;
    private int currentAttackPointIndex = -1;
    private Vector3 currentAttackPoint;
    private float nextAttackPointRefreshTime;

    private static Dictionary<int, Dictionary<int, BuildingAttackSlotManager>> attackPointReservations =
        new Dictionary<int, Dictionary<int, BuildingAttackSlotManager>>();

    public bool HasAttackPoint
    {
        get
        {
            return currentAttackPointIndex >= 0;
        }
    }

    public Vector3 CurrentAttackPoint
    {
        get
        {
            return currentAttackPoint;
        }
    }

    void OnDisable()
    {
        ReleaseAttackPoint();
    }

    void OnDestroy()
    {
        ReleaseAttackPoint();
    }

    public bool TryReserveAttackPoint(
        DamageableBuilding building,
        EnemyAI enemyAI,
        EnemyMovement movement,
        float refreshInterval,
        float buildingMovePointSampleRange,
        float attackPointArriveDistance,
        out Vector3 attackPoint)
    {
        attackPoint = transform.position;

        if (building == null || enemyAI == null || movement == null)
        {
            ReleaseAttackPoint();
            return false;
        }

        int buildingId = building.GetInstanceID();

        CleanupAttackPointReservations(buildingId);

        bool hadAttackPointOnSameBuilding =
            currentAttackBuildingId == buildingId && currentAttackPointIndex >= 0;

        int previousAttackPointIndex = currentAttackPointIndex;
        Vector3 previousAttackPoint = currentAttackPoint;

        if (currentAttackBuildingId == buildingId && currentAttackPointIndex >= 0)
        {
            if (Time.time < nextAttackPointRefreshTime)
            {
                attackPoint = currentAttackPoint;
                return true;
            }

            ReleaseAttackPoint();
        }

        if (currentAttackBuildingId != 0)
        {
            ReleaseAttackPoint();
        }

        List<Vector3> attackPoints = GetAttackPointCandidates(building.gameObject, enemyAI, movement);

        if (attackPoints.Count == 0)
        {
            return false;
        }

        int bestIndex = -1;
        Vector3 bestPoint = transform.position;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < attackPoints.Count; i++)
        {
            if (IsAttackPointReservedByOtherEnemy(buildingId, i))
            {
                continue;
            }

            Vector3 navMeshPoint;

            if (!movement.TryGetNavMeshPoint(attackPoints[i], buildingMovePointSampleRange, out navMeshPoint))
            {
                continue;
            }

            NavMeshPath path;
            Vector3 lastReachablePoint;

            if (!movement.TryGetPath(navMeshPoint, buildingMovePointSampleRange, out path, out lastReachablePoint))
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
                pathLength = Vector3.Distance(transform.position, navMeshPoint);
            }

            float distanceToBuilding = EnemyTargetUtility.GetDistanceToTarget(navMeshPoint, building.gameObject);

            if (distanceToBuilding > GetBuildingAttackReach(enemyAI, movement) + 0.05f)
            {
                continue;
            }

            float score = pathLength + distanceToBuilding * 0.2f;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
                bestPoint = navMeshPoint;
            }
        }

        if (bestIndex < 0)
        {
            Vector3 previousNavMeshPoint;

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

        ReserveAttackPoint(buildingId, bestIndex, bestPoint, refreshInterval);
        attackPoint = bestPoint;
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
        if (building == null || enemyAI == null || movement == null)
        {
            return false;
        }

        if (!IsNearAttackPoint(movement, attackPoint, attackPointArriveDistance))
        {
            if (showDebug)
            {
                float distanceToAttackPoint = Vector3.Distance(transform.position, attackPoint);
                Debug.Log(gameObject.name + " is not near attack point. Distance: " + distanceToAttackPoint + ", required: " + attackPointArriveDistance);
            }

            return false;
        }

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
        if (targetBuilding == null || enemyAI == null)
        {
            return false;
        }

        Vector3 boxCenter;
        Vector3 halfExtents;

        GetFrontAttackBox(enemyAI, attackBoxWidth, attackBoxHeight, out boxCenter, out halfExtents);

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
        if (targetBuilding == null || enemyAI == null)
        {
            return false;
        }

        Vector3 startPoint = GetAttackLineStartPoint(enemyAI);
        Vector3 targetPoint = EnemyTargetUtility.GetClosestPointToTarget(startPoint, targetBuilding.gameObject);
        Vector3 direction = targetPoint - startPoint;
        float distance = direction.magnitude;

        if (distance <= 0.05f)
        {
            return true;
        }

        direction.Normalize();

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

            if (hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

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

        return hitBuilding == targetBuilding;
    }

    public bool IsNearAttackPoint(EnemyMovement movement, Vector3 attackPoint, float attackPointArriveDistance)
    {
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

        if (target == null || enemyAI == null || movement == null || !movement.IsReady)
        {
            return false;
        }

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

        float standDistance = Mathf.Max(enemyAI.attackRange * 0.8f, movement.Radius + 0.3f);

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

            if (path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float pathLength = movement.GetPathLength(path);

            if (pathLength <= 0f)
            {
                pathLength = Vector3.Distance(transform.position, navMeshPoint);
            }

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
        if (currentAttackBuildingId == 0 || currentAttackPointIndex < 0)
        {
            return;
        }

        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

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

        currentAttackBuildingId = 0;
        currentAttackPointIndex = -1;
        currentAttackPoint = Vector3.zero;
        nextAttackPointRefreshTime = 0f;
    }

    public void DrawCurrentAttackPointGizmos(float attackPointArriveDistance)
    {
        if (currentAttackPointIndex < 0)
        {
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
        if (currentAttackBuildingId == buildingId && currentAttackPointIndex == attackPointIndex)
        {
            currentAttackPoint = attackPoint;
            nextAttackPointRefreshTime = Time.time + refreshInterval;
            return;
        }

        ReleaseAttackPoint();

        if (!attackPointReservations.ContainsKey(buildingId))
        {
            attackPointReservations[buildingId] = new Dictionary<int, BuildingAttackSlotManager>();
        }

        attackPointReservations[buildingId][attackPointIndex] = this;

        currentAttackBuildingId = buildingId;
        currentAttackPointIndex = attackPointIndex;
        currentAttackPoint = attackPoint;
        nextAttackPointRefreshTime = Time.time + refreshInterval;
    }

    bool IsAttackPointReservedByOtherEnemy(int buildingId, int attackPointIndex)
    {
        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        if (!attackPointReservations.TryGetValue(buildingId, out buildingReservations))
        {
            return false;
        }

        BuildingAttackSlotManager reservedEnemy;

        if (!buildingReservations.TryGetValue(attackPointIndex, out reservedEnemy))
        {
            return false;
        }

        if (reservedEnemy == null || !reservedEnemy.isActiveAndEnabled)
        {
            buildingReservations.Remove(attackPointIndex);
            return false;
        }

        return reservedEnemy != this;
    }

    void CleanupAttackPointReservations(int buildingId)
    {
        Dictionary<int, BuildingAttackSlotManager> buildingReservations;

        if (!attackPointReservations.TryGetValue(buildingId, out buildingReservations))
        {
            return;
        }

        List<int> removeIndexes = null;

        foreach (KeyValuePair<int, BuildingAttackSlotManager> reservation in buildingReservations)
        {
            BuildingAttackSlotManager reservedEnemy = reservation.Value;

            if (reservedEnemy != null && reservedEnemy.isActiveAndEnabled)
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
            attackPointReservations.Remove(buildingId);
        }
    }

    void GetFrontAttackBox(EnemyAI enemyAI, float attackBoxWidth, float attackBoxHeight, out Vector3 boxCenter, out Vector3 halfExtents)
    {
        float length = Mathf.Max(0.1f, enemyAI.attackRange);
        float width = Mathf.Max(0.1f, attackBoxWidth);
        float height = Mathf.Max(0.1f, attackBoxHeight);
        float bodyForwardOffset = GetBodyForwardOffset(enemyAI);

        boxCenter =
            transform.position +
            transform.forward * (bodyForwardOffset + length * 0.5f) +
            Vector3.up * (height * 0.5f);

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
        return enemyAI.attackRange + bodyRadius;
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

    List<Vector3> GetAttackPointCandidates(GameObject target, EnemyAI enemyAI, EnemyMovement movement)
    {
        List<Vector3> attackPoints = new List<Vector3>();

        BuildingAttackPoints manualAttackPoints = target.GetComponentInChildren<BuildingAttackPoints>();

        if (manualAttackPoints != null && manualAttackPoints.attackPoints != null)
        {
            for (int i = 0; i < manualAttackPoints.attackPoints.Length; i++)
            {
                if (manualAttackPoints.attackPoints[i] == null)
                {
                    continue;
                }

                attackPoints.Add(manualAttackPoints.attackPoints[i].position);
            }
        }

        if (attackPoints.Count > 0)
        {
            return attackPoints;
        }

        AddAutoAttackPointCandidates(target, enemyAI, movement, attackPoints);
        return attackPoints;
    }

    void AddAutoAttackPointCandidates(GameObject target, EnemyAI enemyAI, EnemyMovement movement, List<Vector3> attackPoints)
    {
        Bounds bounds;

        if (!EnemyTargetUtility.TryGetTargetBounds(target, out bounds))
        {
            attackPoints.Add(GetMovePointNearTarget(target, movement, 2f));
            return;
        }

        float standDistance = Mathf.Max(enemyAI.attackRange * 0.8f, movement.Radius + 0.4f);
        float pointSpacing = Mathf.Max(movement.Radius * 2.5f, 1.2f);
        float y = transform.position.y;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        AddAttackPointsOnLine(attackPoints, new Vector3(min.x, y, min.z - standDistance), new Vector3(max.x, y, min.z - standDistance), pointSpacing);
        AddAttackPointsOnLine(attackPoints, new Vector3(min.x, y, max.z + standDistance), new Vector3(max.x, y, max.z + standDistance), pointSpacing);
        AddAttackPointsOnLine(attackPoints, new Vector3(min.x - standDistance, y, min.z), new Vector3(min.x - standDistance, y, max.z), pointSpacing);
        AddAttackPointsOnLine(attackPoints, new Vector3(max.x + standDistance, y, min.z), new Vector3(max.x + standDistance, y, max.z), pointSpacing);
    }

    void AddAttackPointsOnLine(List<Vector3> attackPoints, Vector3 startPoint, Vector3 endPoint, float pointSpacing)
    {
        float length = Vector3.Distance(startPoint, endPoint);
        int pointCount = Mathf.Max(1, Mathf.CeilToInt(length / pointSpacing) + 1);

        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount == 1 ? 0.5f : (float)i / (pointCount - 1);
            attackPoints.Add(Vector3.Lerp(startPoint, endPoint, t));
        }
    }
}
