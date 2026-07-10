using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("적군 설정")]
    public int health = 30;
    public float moveSpeed = 3f;
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    private float lastAttackTime;

    private NavMeshAgent agent;
    private Core core;

    [Header("전기 타워 관한 변수")]
    public int curParalyzeStack; //현재 마비 stack 수
    public int maxParalyzeStack = 100; //마비상태 들어가려면 필요한 미비 stack 수
    public float paralyzeDefensePower = 1f; //보스,엘리트 적 미비 저항력
    private float paralyzeEndTime; //마비 상태 끝나는 시간
    private bool isParalyzed; //마비 상태인지 여부
    private bool isSlowed; //감속 상태인지 여부
    private float slowEndTime; //감속 상태 끝나는 시간
    private float currentSlowPower; //감속 상태에서 현재 감속력
    private Collider coreCollider;


    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = moveSpeed;
        core = FindObjectOfType<Core>();
        if (core != null)
        {
            coreCollider = core.GetComponentInChildren<Collider>();
        }

    }

    void Update()
    {
        if (HandleParalyzeState())
        {
            return; //마비 상태면 이동, 공격 못함
        }

        HandleSlowState(); //감속 상태 처리
        EnemyDafaultLogic();
    }

    //감속 상태 처리
    void HandleSlowState()
    {
        if (!isSlowed)
        {
            return;
        }

        if (Time.time >= slowEndTime)
        {
            isSlowed = false;
            currentSlowPower = 0f; //감속력 초기화

            ResetSpeed();
        }
    }

    //마비 상태 판단
    bool HandleParalyzeState()
    {
        if (!isParalyzed)
        {
            return false;
        }

        if (Time.time >= paralyzeEndTime)
        {
            isParalyzed = false;

            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;
            }

            return false;
        }

        return true;
    }

    //적의 기본 로직: 건축물 공격, 없으면 코어 공격
    public void EnemyDafaultLogic()
    {
        // 1. 공격 범위 내에 BuildingObject가 있는지 탐지
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attackRange);
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

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                targetBuilding.GetDamage(attackDamage);
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

            if (distance <= attackRange)
            {
                if (agent != null && agent.enabled)
                {
                    agent.isStopped = true;
                }

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    core.GetDamage(attackDamage);
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

    //마비 상태 stack 증가
    public void AddParalyzeStack(int amount, float duration)
    {
        if (isParalyzed)
        {
            return;
        }

        curParalyzeStack += amount;

        if (curParalyzeStack >= maxParalyzeStack)
        {
            curParalyzeStack = maxParalyzeStack;
            //마비상태 들어가기
            EnterParalyze(duration);
        }
    }

    //마비 상태 들어가기
    void EnterParalyze(float duration)
    {
        isParalyzed = true;
        curParalyzeStack = 0;
        paralyzeEndTime = Time.time + duration;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
    }

    //다매지 받기
    public void TakeDamage(int defaultDamage)
    {
        health -= defaultDamage;
        if (health <= 0) Destroy(gameObject);
    }

    //속도 감소
    public void SetSpeedDown(float coldPower, float coldTime)
    {
        if (agent == null || !agent.enabled)
        {
            return;
        }

        isSlowed = true;

        if (coldPower >= currentSlowPower) //새로운 감속력이 현재 감속력보다 크면
        {
            currentSlowPower = coldPower; //현재 감속력 업데이트
            agent.speed = moveSpeed * (1f - currentSlowPower); //감속력 적용
            slowEndTime = Time.time + coldTime; //감속 상태 끝나는 시간 업데이트
        }
    }


    //속도 회복
    void ResetSpeed()
    {
        if (agent != null && agent.enabled)
        {
            agent.speed = moveSpeed;
        }
    }
}