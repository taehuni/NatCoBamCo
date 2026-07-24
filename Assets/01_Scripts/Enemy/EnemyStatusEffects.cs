using UnityEngine;
using UnityEngine.AI;

public class EnemyStatusEffects : MonoBehaviour
{
    private EnemyAI enemyAI;
    private NavMeshAgent agent;

    private float paralyzeEndTime;
    private bool isParalyzed;

    private bool isSlowed;
    private float slowEndTime;
    private float currentSlowPower;

    public bool IsParalyzed
    {
        get { return isParalyzed; }
    }

    public bool IsSlowed
    {
        get { return isSlowed; }
    }

    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
        agent = GetComponent<NavMeshAgent>();
    }

    public bool TickParalyzeState()
    {
        if (!isParalyzed)
        {
            return false;
        }

        if (Time.time < paralyzeEndTime)
        {
            return true;
        }

        isParalyzed = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        return false;
    }

    public void TickSlowState()
    {
        if (!isSlowed)
        {
            return;
        }

        if (Time.time < slowEndTime)
        {
            return;
        }

        isSlowed = false;
        currentSlowPower = 0f;

        ResetSpeed();
    }

    // 减速。新的减速更强时才覆盖旧减速
    // 감속. 새로운 감속 효과가 더 강할 때만 기존 감속을 덮어씀
    public void SetSpeedDown(float slowPower, float slowTime)
    {
        if (enemyAI == null || agent == null || !agent.enabled)
        {
            return;
        }

        isSlowed = true;

        if (slowPower < currentSlowPower)
        {
            return;
        }

        currentSlowPower = slowPower;
        agent.speed = enemyAI.moveSpeed * (1f - currentSlowPower);
        slowEndTime = Time.time + slowTime;
    }

    // 麻痹层数增加，达到最大层数后进入麻痹
    // 마비 스택 증가. 최대 스택에 도달하면 마비 상태 진입
    public void AddParalyzeStack(float amount, float duration)
    {
        if (enemyAI == null || isParalyzed)
        {
            return;
        }

        enemyAI.curParalyzeStack += amount * (1f - enemyAI.paralyzeDefensePower);

        if (enemyAI.curParalyzeStack < enemyAI.maxParalyzeStack)
        {
            return;
        }

        enemyAI.curParalyzeStack = enemyAI.maxParalyzeStack;
        EnterParalyze(duration);
    }

    void EnterParalyze(float duration)
    {
        isParalyzed = true;
        enemyAI.curParalyzeStack = 0f;
        paralyzeEndTime = Time.time + duration;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
    }

    void ResetSpeed()
    {
        if (enemyAI == null || agent == null || !agent.enabled)
        {
            return;
        }

        agent.speed = enemyAI.moveSpeed;
    }
}
