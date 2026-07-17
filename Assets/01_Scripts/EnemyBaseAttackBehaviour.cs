using UnityEngine;
using UnityEngine.AI;

public class EnemyBaseAttackBehaviour : MonoBehaviour
{
    public float lastAttackTime;
    private NavMeshAgent agent;
    private Core core;
    private Collider coreCollider;
    private EnemyAI enemyAI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();
        if (enemyAI != null && agent != null)
        {
            agent.speed = enemyAI.moveSpeed;
        }
        core = FindObjectOfType<Core>();
        if (core != null)
        {
            coreCollider = core.GetComponentInChildren<Collider>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (enemyAI == null)
        {
            return;
        }

        if (enemyAI.IsParalyzed())
        {
            return;
        }
        EnemyDefaultLogic();
    }


    //적의 기본 로직: 건축물 공격, 없으면 코어 공격
    public void EnemyDefaultLogic()
    {
        // 1. 공격 범위 내에 BuildingObject가 있는지 탐지
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, enemyAI.attackRange);
        DamageableBuilding targetBuilding = null;

        foreach (var hit in hitColliders)
        {
            DamageableBuilding building = hit.GetComponentInParent<DamageableBuilding>();

            //주변 에 건축물이 있으면 그걸 타겟으로 설정하고 break
            if (building != null)
            {
                targetBuilding = building;
                break;
            }
        }
        // 2. 건축물이 있으면 공격
        if (targetBuilding != null)
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
            }

            if (Time.time >= lastAttackTime + enemyAI.attackCooldown)
            {
                targetBuilding.GetDamage(enemyAI.attackDamage);
                lastAttackTime = Time.time;
            }
        }
        // 3. 없으면 코어를 향해 이동하거나 공격
        else if (core != null)
        {

            float distance;

            if (coreCollider != null)
            {
                Vector3 closestPoint = coreCollider.ClosestPoint(transform.position);
                distance = Vector3.Distance(transform.position, closestPoint);
            }
            else
            {
                distance = Vector3.Distance(transform.position, core.transform.position);
            }

            if (distance <= enemyAI.attackRange)
            {
                if (agent != null && agent.enabled)
                {
                    agent.isStopped = true;
                }

                if (Time.time >= lastAttackTime + enemyAI.attackCooldown)
                {
                    core.GetDamage(enemyAI.attackDamage);
                    lastAttackTime = Time.time;
                }
            }
            else
            {
                if (agent != null && agent.enabled)
                {
                    agent.isStopped = false;
                    agent.SetDestination(core.transform.position);
                }
            }
        }
    }
}
