using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private float defaultStoppingDistance;

    public NavMeshAgent Agent
    {
        get
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            return agent;
        }
    }

    public bool IsReady
    {
        get
        {
            return Agent != null && Agent.enabled && Agent.isOnNavMesh;
        }
    }

    public float Radius
    {
        get
        {
            if (Agent == null)
            {
                return 0f;
            }

            return Agent.radius;
        }
    }

    public float StoppingDistance
    {
        get
        {
            if (Agent == null)
            {
                return 0f;
            }

            return Agent.stoppingDistance;
        }
    }

    public void Initialize(EnemyAI enemyAI)
    {
        if (enemyAI == null || Agent == null)
        {
            return;
        }

        Agent.speed = enemyAI.moveSpeed;
        defaultStoppingDistance = Agent.stoppingDistance;
        Agent.avoidancePriority = Random.Range(30, 71);
        SetAutoRotation(true);
    }

    public void MoveToPosition(Vector3 position, float navMeshSampleRange)
    {
        MoveToPosition(position, navMeshSampleRange, defaultStoppingDistance);
    }

    public void MoveToPosition(Vector3 position, float navMeshSampleRange, float stoppingDistance)
    {
        if (!IsReady)
        {
            return;
        }

        Vector3 navMeshPosition;

        if (!TryGetNavMeshPoint(position, navMeshSampleRange, out navMeshPosition))
        {
            return;
        }

        Agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        SetAutoRotation(true);
        Agent.isStopped = false;
        Agent.SetDestination(navMeshPosition);
    }

    public void Stop()
    {
        if (!IsReady)
        {
            return;
        }

        Agent.isStopped = true;
    }

    public void SetAutoRotation(bool useAutoRotation)
    {
        if (Agent == null)
        {
            return;
        }

        Agent.updateRotation = useAutoRotation;
    }

    public void FacePoint(Vector3 point, float turnSpeed)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    public void FacePointInstant(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    public bool TryGetNavMeshPoint(Vector3 sourcePoint, float sampleRange, out Vector3 navMeshPoint)
    {
        NavMeshHit hit;

        if (NavMesh.SamplePosition(sourcePoint, out hit, sampleRange, NavMesh.AllAreas))
        {
            navMeshPoint = hit.position;
            return true;
        }

        navMeshPoint = sourcePoint;
        return false;
    }

    public bool TryGetPath(
        Vector3 destination,
        float navMeshSampleRange,
        out NavMeshPath path,
        out Vector3 lastReachablePoint)
    {
        Vector3 navMeshDestination;
        return TryGetPath(destination, navMeshSampleRange, out path, out lastReachablePoint, out navMeshDestination);
    }

    public bool TryGetPath(
        Vector3 destination,
        float navMeshSampleRange,
        out NavMeshPath path,
        out Vector3 lastReachablePoint,
        out Vector3 navMeshDestination)
    {
        path = new NavMeshPath();
        lastReachablePoint = transform.position;
        navMeshDestination = destination;

        if (!IsReady)
        {
            return false;
        }

        if (!TryGetNavMeshPoint(destination, navMeshSampleRange, out navMeshDestination))
        {
            return false;
        }

        bool hasPath = Agent.CalculatePath(navMeshDestination, path);

        if (path.corners != null && path.corners.Length > 0)
        {
            lastReachablePoint = path.corners[path.corners.Length - 1];
        }

        return hasPath;
    }

    public float GetPathLength(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return length;
    }
}
