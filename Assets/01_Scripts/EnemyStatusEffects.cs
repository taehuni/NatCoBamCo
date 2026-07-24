using UnityEngine;
using UnityEngine.AI;

// 状态异常模块：负责减速、麻痹这些会影响敌人行动的效果。
// 상태 이상 모듈: 감속, 마비처럼 적의 행동에 영향을 주는 효과를 담당함.
public class EnemyStatusEffects : MonoBehaviour
{
    private EnemyAI enemyAI;
    private NavMeshAgent agent;

    // 麻痹结束时间和当前是否麻痹。
    // 마비 종료 시간과 현재 마비 상태 여부.
    private float paralyzeEndTime;
    private bool isParalyzed;

    // 减速状态、减速结束时间、当前减速强度。
    // 감속 상태, 감속 종료 시간, 현재 감속 강도.
    private bool isSlowed;
    private float slowEndTime;
    private float currentSlowPower;

    // 对外提供是否麻痹的只读入口。
    // 외부에 현재 마비 상태인지 읽기 전용으로 제공.
    public bool IsParalyzed
    {
        get { return isParalyzed; }
    }

    // 对外提供是否减速的只读入口。
    // 외부에 현재 감속 상태인지 읽기 전용으로 제공.
    public bool IsSlowed
    {
        get { return isSlowed; }
    }

    // 初始化时保存 EnemyAI 和 NavMeshAgent 引用。
    // 초기화 시 EnemyAI와 NavMeshAgent 참조를 저장.
    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
        agent = GetComponent<NavMeshAgent>();
    }

    // 每帧检查麻痹状态。返回 true 表示敌人仍处于麻痹中，外部逻辑应该停止继续执行。
    // 매 프레임 마비 상태를 확인. true를 반환하면 아직 마비 중이므로 외부 로직은 계속 실행하지 않아야 함.
    public bool TickParalyzeState()
    {
        if (!isParalyzed)
        {
            return false;
        }

        // 当前时间还没到结束时间，说明麻痹仍在持续。
        // 현재 시간이 종료 시간보다 작으면 마비가 아직 지속 중.
        if (Time.time < paralyzeEndTime)
        {
            return true;
        }

        // 麻痹结束，恢复 NavMeshAgent 移动。
        // 마비 종료, NavMeshAgent 이동을 다시 허용.
        isParalyzed = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        return false;
    }

    // 每帧检查减速是否结束，结束后恢复原始速度。
    // 매 프레임 감속이 끝났는지 확인하고, 끝나면 원래 속도로 복구.
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

        // 减速时间结束，清空减速状态。
        // 감속 시간이 끝났으므로 감속 상태를 초기화.
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

        // 如果新的减速更弱，就不覆盖当前强减速。
        // 새 감속이 현재 감속보다 약하면 현재 강한 감속을 덮어쓰지 않음.
        if (slowPower < currentSlowPower)
        {
            return;
        }

        // 根据减速强度修改 NavMeshAgent 速度。
        // 감속 강도에 따라 NavMeshAgent 속도를 변경.
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

        // 麻痹抗性越高，本次实际增加的麻痹层数越少。
        // 마비 저항이 높을수록 이번에 실제로 증가하는 마비 스택이 줄어듦.
        enemyAI.curParalyzeStack += amount * (1f - enemyAI.paralyzeDefensePower);

        // 层数还没满时，只累积层数，不进入麻痹。
        // 스택이 아직 최대치가 아니면 스택만 누적하고 마비에 들어가지 않음.
        if (enemyAI.curParalyzeStack < enemyAI.maxParalyzeStack)
        {
            return;
        }

        // 达到最大层数后进入麻痹，并把层数锁到最大值。
        // 최대 스택에 도달하면 마비에 들어가고 스택을 최대값으로 고정.
        enemyAI.curParalyzeStack = enemyAI.maxParalyzeStack;
        EnterParalyze(duration);
    }

    // 进入麻痹状态：清空层数、记录结束时间、停止 NavMeshAgent。
    // 마비 상태 진입: 스택 초기화, 종료 시간 기록, NavMeshAgent 정지.
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

    // 恢复到 EnemyAI 中配置的基础移动速度。
    // EnemyAI에 설정된 기본 이동 속도로 복구.
    void ResetSpeed()
    {
        if (enemyAI == null || agent == null || !agent.enabled)
        {
            return;
        }

        agent.speed = enemyAI.moveSpeed;
    }
}
