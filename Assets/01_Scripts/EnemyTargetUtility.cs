using UnityEngine;

public static class EnemyTargetUtility
{
    public static float GetDistanceToTarget(Vector3 sourcePoint, GameObject target)
    {
        if (target == null)
        {
            return Mathf.Infinity;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
            {
                continue;
            }

            Vector3 closestPoint = col.ClosestPoint(sourcePoint);
            float distance = Vector3.Distance(sourcePoint, closestPoint);

            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        if (closestDistance != Mathf.Infinity)
        {
            return closestDistance;
        }

        return Vector3.Distance(sourcePoint, target.transform.position);
    }

    public static Vector3 GetClosestPointToTarget(Vector3 sourcePoint, GameObject target)
    {
        if (target == null)
        {
            return sourcePoint;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        Vector3 closestPoint = target.transform.position;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
            {
                continue;
            }

            Vector3 point = col.ClosestPoint(sourcePoint);
            float distance = Vector3.Distance(sourcePoint, point);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    public static bool TryGetTargetBounds(GameObject target, out Bounds bounds)
    {
        bounds = new Bounds(target.transform.position, Vector3.zero);

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null || col.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds;
    }
}
