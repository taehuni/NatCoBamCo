using UnityEngine;
using UnityEngine.AI;

// EnemyMovement.cs 와 같은 역할(NavMeshAgent 래퍼)이지만 EnemyAI 에 의존하지 않는
// 생존자 전용 버전. EnemyMovement 는 Initialize(EnemyAI) 로 moveSpeed 를 가져오는 구조라
// 그대로 재사용할 수 없어서 별도로 분리함.
[RequireComponent(typeof(NavMeshAgent))]
public class SurvivorMovement : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public float stoppingDistance = 1.5f;

    private NavMeshAgent agent;

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
        get { return Agent != null && Agent.enabled && Agent.isOnNavMesh; }
    }

    void Awake()
    {
        Agent.speed = moveSpeed;
        Agent.stoppingDistance = stoppingDistance;
    }

    public void MoveToPosition(Vector3 position)
    {
        if (!IsReady)
        {
            return;
        }

        Agent.isStopped = false;
        Agent.SetDestination(position);
    }

    public void Stop()
    {
        if (!IsReady)
        {
            return;
        }

        Agent.isStopped = true;
    }

    public bool HasArrived()
    {
        if (!IsReady || Agent.pathPending)
        {
            return false;
        }

        return Agent.remainingDistance <= Agent.stoppingDistance;
    }
}
