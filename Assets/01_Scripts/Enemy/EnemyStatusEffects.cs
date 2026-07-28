using UnityEngine;
using UnityEngine.AI;

// 敌人状态异常模块：负责减速、麻痹层数、麻痹停止移动。
// 적 상태 이상 모듈: 감속, 마비 스택, 마비로 인한 이동 정지를 담당한다.
// 这里不负责伤害，只负责“敌人当前处于什么状态”以及状态到期恢复。
// 여기서는 피해를 처리하지 않고, 적이 현재 어떤 상태인지와 상태 종료 복구만 처리한다.
// 状态异常模块：负责减速、麻痹层数、麻痹停止。
public class EnemyStatusEffects : MonoBehaviour
{
    [Header("Paralyze Data / 마비 데이터")]
    public float curParalyzeStack;
    public float maxParalyzeStack;
    public float paralyzeDefensePower;

    private NavMeshAgent agent;
    private EnemyMovement movement;

    private float paralyzeEndTime;
    private bool isParalyzed;

    private bool isSlowed;
    private float slowEndTime;
    private float currentSlowPower;

    // 只读入口：外部可以知道敌人是否麻痹，但不能直接改 isParalyzed。
    // 읽기 전용 입구: 외부는 마비 여부를 알 수 있지만 isParalyzed를 직접 바꿀 수 없다.
    public bool IsParalyzed
    {
        get { return isParalyzed; }
    }

    // 只读入口：外部可以知道敌人是否减速。
    // 읽기 전용 입구: 외부는 감속 여부를 확인할 수 있다.
    public bool IsSlowed
    {
        get { return isSlowed; }
    }

    public void Initialize(EnemyAI enemyAI)
    {
        // 缓存 NavMeshAgent，用来控制移动速度和停止移动。
        // NavMeshAgent를 캐시해서 이동 속도와 이동 정지를 제어한다.
        agent = GetComponent<NavMeshAgent>();
        // 缓存 EnemyMovement，用来读取敌人的基础移动速度。
        // EnemyMovement를 캐시해서 적의 기본 이동 속도를 읽는다.
        movement = GetComponent<EnemyMovement>();
    }

    public bool TickParalyzeState()
    {
        // 没有麻痹时返回 false，表示外部逻辑可以继续执行。
        // 마비 상태가 아니면 false를 반환해서 외부 로직이 계속 실행되게 한다.
        if (!isParalyzed)
        {
            return false;
        }

        // 麻痹时间还没结束，返回 true，让 EnemyAI.Update 直接 return。
        // 마비 시간이 끝나지 않았으면 true를 반환해서 EnemyAI.Update가 바로 return하게 한다.
        if (Time.time < paralyzeEndTime)
        {
            return true;
        }

        // 麻痹结束，恢复移动。
        // 마비가 끝나면 이동을 다시 허용한다.
        isParalyzed = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        return false;
    }

    public void TickSlowState()
    {
        // 没有减速状态时不做任何处理。
        // 감속 상태가 아니면 아무 처리도 하지 않는다.
        if (!isSlowed)
        {
            return;
        }

        // 减速时间还没结束，继续保持当前速度。
        // 감속 시간이 끝나지 않았으면 현재 속도를 유지한다.
        if (Time.time < slowEndTime)
        {
            return;
        }

        // 减速结束，清空减速数据并恢复基础速度。
        // 감속이 끝나면 감속 데이터를 초기화하고 기본 속도로 되돌린다.
        isSlowed = false;
        currentSlowPower = 0f;

        ResetSpeed();
    }

    public void SetSpeedDown(float slowPower, float slowTime)
    {
        // Agent 不存在或者被禁用时，不能改速度。
        // Agent가 없거나 비활성화되어 있으면 속도를 바꿀 수 없다.
        if (agent == null || !agent.enabled)
        {
            return;
        }

        // 只让更强的减速覆盖更弱的减速，避免弱减速刷新强减速。
        // 더 강한 감속만 약한 감속을 덮어쓴다. 약한 감속이 강한 감속을 갱신하지 못하게 한다.
        if (slowPower < currentSlowPower)
        {
            return;
        }

        // slowPower 是减速比例。例如 0.3 表示速度变成 70%。
        // slowPower는 감속 비율이다. 예: 0.3이면 속도가 70%가 된다.
        isSlowed = true;
        currentSlowPower = slowPower;
        agent.speed = GetBaseMoveSpeed() * (1f - currentSlowPower);
        // 记录减速结束时间。
        // 감속 종료 시간을 기록한다.
        slowEndTime = Time.time + slowTime;
    }

    public void AddParalyzeStack(float amount, float duration)
    {
        // 已经麻痹时不继续叠层，避免重复进入麻痹。
        // 이미 마비 상태라면 스택을 더 쌓지 않는다. 중복 마비 진입을 막기 위해서다.
        if (isParalyzed)
        {
            return;
        }

        // 实际增加的麻痹值会被 paralyzeDefensePower 抵抗一部分。
        // 실제로 증가하는 마비 수치는 paralyzeDefensePower에 의해 일부 저항된다.
        curParalyzeStack += amount * (1f - paralyzeDefensePower);

        // 层数没满就继续累积，不进入麻痹。
        // 스택이 가득 차지 않았으면 계속 누적하고 마비에 들어가지 않는다.
        if (curParalyzeStack < maxParalyzeStack)
        {
            return;
        }

        // 层数达到上限，进入麻痹状态。
        // 스택이 최대치에 도달하면 마비 상태로 들어간다.
        curParalyzeStack = maxParalyzeStack;
        EnterParalyze(duration);
    }

    void EnterParalyze(float duration)
    {
        // 进入麻痹后清空层数，并记录麻痹结束时间。
        // 마비에 들어가면 스택을 비우고 마비 종료 시간을 기록한다.
        isParalyzed = true;
        curParalyzeStack = 0f;
        paralyzeEndTime = Time.time + duration;

        // 停止 NavMeshAgent，让敌人原地不动。
        // NavMeshAgent를 멈춰서 적이 제자리에서 움직이지 않게 한다.
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
    }

    void ResetSpeed()
    {
        // 恢复速度前先确认 Agent 可用。
        // 속도를 복구하기 전에 Agent가 사용 가능한지 확인한다.
        if (agent == null || !agent.enabled)
        {
            return;
        }

        agent.speed = GetBaseMoveSpeed();
    }

    float GetBaseMoveSpeed()
    {
        // movement 可能因为初始化顺序问题为空，所以这里再尝试获取一次。
        // 초기화 순서 때문에 movement가 비어 있을 수 있어서 여기서 한 번 더 가져온다.
        if (movement == null)
        {
            movement = GetComponent<EnemyMovement>();
        }

        if (movement == null)
        {
            // 如果没有 EnemyMovement，就用 Agent 当前速度兜底。
            // EnemyMovement가 없으면 Agent의 현재 속도를 예외 처리 값으로 사용한다.
            return agent == null ? 0f : agent.speed;
        }

        return movement.moveSpeed;
    }
}
