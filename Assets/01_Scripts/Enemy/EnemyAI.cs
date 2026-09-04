using UnityEngine;

// 敌人总入口脚本：负责把 EnemyHealth、EnemyDefense、EnemyMovement 等模块连接起来。
// 적의 메인 입구 스크립트: EnemyHealth, EnemyDefense, EnemyMovement 같은 모듈들을 서로 연결한다.
// 外部脚本优先通过 EnemyAI 调用常用功能，这样外部不用关心敌人内部到底拆成了多少个组件。
// 외부 스크립트는 되도록 EnemyAI를 통해 공통 기능을 호출한다. 그러면 내부 컴포넌트 구조를 몰라도 된다.

// 敌人的总入口脚本。
// EnemyAI itself no longer stores enemy data directly.
// It only connects modules together and provides module access entries such as enemyAI.Health.
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyDefense))]
[RequireComponent(typeof(EnemyStatusEffects))]
[RequireComponent(typeof(EnemyDeathHandler))]
[RequireComponent(typeof(EnemyClassTraits))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyAttack))]
[RequireComponent(typeof(EnemyTargetSelector))]
[RequireComponent(typeof(EnemyPathBlockHandler))]
// 同时实现 IDamageable，让通用伤害系统可以通过统一接口攻击敌人。
// IDamageable도 구현하여 공용 피해 시스템이 동일한 인터페이스로 적에게 피해를 줄 수 있게 한다.
public class EnemyAI : MonoBehaviour, IDamageable
{
    // 敌人等级：普通、精英、Boss。主要给掉落、技能、特殊增伤等系统判断用。
    // 적 등급: 일반, 엘리트, 보스. 드롭, 스킬, 특수 추가 피해 판단에 사용한다.
    public enum EnemyGrade
    {
        Normal,
        Elite,
        Boss
    }

    // 敌人种类：决定敌人的行为倾向，例如高速型优先玩家、坦克型优先墙。
    // 적 종류: 행동 성향을 결정한다. 예: Fast는 플레이어 우선, Tank는 벽 우선.
    public enum EnemyClass
    {
        Standard,
        Fast,
        Tank,
        Ranged
    }

    private EnemyHealth enemyHealth;
    private EnemyDefense enemyDefense;
    private EnemyStatusEffects statusEffects;
    private EnemyDeathHandler deathHandler;
    private EnemyClassTraits classTraits;
    private EnemyMovement movement;
    private EnemyAttack enemyAttack;
    private EnemyTargetSelector targetSelector;
    private EnemyPathBlockHandler pathBlockHandler;

    // 下面这些大写属性是“模块访问入口”。
    // 아래 대문자로 시작하는 속성들은 "모듈 접근 입구"다.
    // 例如 enemyAI.Health 得到的是挂在同一个敌人身上的 EnemyHealth 组件引用。
    // 예를 들어 enemyAI.Health는 같은 적 오브젝트에 붙어 있는 EnemyHealth 컴포넌트 참조를 돌려준다.
    // 如果缓存为空，就重新 GetComponent 一次，避免因为初始化顺序导致空引用。
    // 캐시가 비어 있으면 GetComponent로 다시 찾는다. 초기화 순서 문제로 null이 되는 것을 줄이기 위해서다.
    public EnemyHealth Health
    {
        get
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }

            return enemyHealth;
        }
    }

    public EnemyDefense Defense
    {
        get
        {
            if (enemyDefense == null)
            {
                enemyDefense = GetComponent<EnemyDefense>();
            }

            return enemyDefense;
        }
    }

    public EnemyStatusEffects StatusEffects
    {
        get
        {
            if (statusEffects == null)
            {
                statusEffects = GetComponent<EnemyStatusEffects>();
            }

            return statusEffects;
        }
    }

    public EnemyDeathHandler DeathHandler
    {
        get
        {
            if (deathHandler == null)
            {
                deathHandler = GetComponent<EnemyDeathHandler>();
            }

            return deathHandler;
        }
    }

    public EnemyClassTraits ClassTraits
    {
        get
        {
            if (classTraits == null)
            {
                classTraits = GetComponent<EnemyClassTraits>();
            }

            return classTraits;
        }
    }

    public EnemyMovement Movement
    {
        get
        {
            if (movement == null)
            {
                movement = GetComponent<EnemyMovement>();
            }

            return movement;
        }
    }

    public EnemyAttack AttackModule
    {
        get
        {
            if (enemyAttack == null)
            {
                enemyAttack = GetComponent<EnemyAttack>();
            }

            return enemyAttack;
        }
    }

    public EnemyTargetSelector TargetSelector
    {
        get
        {
            if (targetSelector == null)
            {
                targetSelector = GetComponent<EnemyTargetSelector>();
            }

            return targetSelector;
        }
    }

    public EnemyPathBlockHandler PathBlockHandler
    {
        get
        {
            if (pathBlockHandler == null)
            {
                pathBlockHandler = GetComponent<EnemyPathBlockHandler>();
            }

            return pathBlockHandler;
        }
    }

    void Awake()
    {
        // Awake 阶段把需要的模块全部拿到；如果没挂，就自动添加。
        // Awake 단계에서 필요한 모듈을 모두 가져온다. 없으면 자동으로 추가한다.
        // 这样敌人 prefab 漏挂某个模块时，基础逻辑不会直接崩掉。
        // 이렇게 하면 적 프리팹에 모듈 하나가 빠져도 기본 로직이 바로 깨지는 것을 줄일 수 있다.
        enemyHealth = GetOrAddComponent<EnemyHealth>();
        enemyDefense = GetOrAddComponent<EnemyDefense>();
        statusEffects = GetOrAddComponent<EnemyStatusEffects>();
        deathHandler = GetOrAddComponent<EnemyDeathHandler>();
        classTraits = GetOrAddComponent<EnemyClassTraits>();
        movement = GetOrAddComponent<EnemyMovement>();
        enemyAttack = GetOrAddComponent<EnemyAttack>();
        targetSelector = GetOrAddComponent<EnemyTargetSelector>();
        pathBlockHandler = GetOrAddComponent<EnemyPathBlockHandler>();

        // 把 EnemyAI 自己传给每个模块，让模块可以在需要时反向访问敌人总入口。
        // EnemyAI 자신을 각 모듈에 넘겨서, 모듈이 필요할 때 메인 입구를 다시 참조할 수 있게 한다.
        enemyHealth.Initialize(this);
        enemyDefense.Initialize(this);
        statusEffects.Initialize(this);
        deathHandler.Initialize(this);
        classTraits.Initialize(this);
        movement.Initialize(this);
        enemyAttack.Initialize(this, movement);
    }

    void Start()
    {
        // Start 阶段重置运行时数据，例如当前血量、防御 Debuff 状态。
        // Start 단계에서 현재 체력, 방어력 디버프 상태 같은 런타임 데이터를 초기화한다.
        ResetRuntimeData();
    }

    public void ResetRuntimeData()
    {
        // 血量模块自己负责把当前血量恢复到最大血量。
        // 체력 모듈이 현재 체력을 최대 체력으로 복구한다.
        if (Health != null)
        {
            Health.ResetHealth();
        }

        // 防御模块自己负责记录原始防御力，并清空减防状态。
        // 방어 모듈이 원래 방어력을 기록하고 방어력 감소 상태를 초기화한다.
        if (Defense != null)
        {
            Defense.ResetDefenseData();
        }
    }

    void Update()
    {
        // 麻痹状态优先级最高：如果敌人正在麻痹，当前帧后续状态逻辑直接不走。
        // 마비 상태의 우선순위가 가장 높다. 마비 중이면 이번 프레임의 나머지 상태 처리를 건너뛴다.
        if (StatusEffects != null && StatusEffects.TickParalyzeState())
        {
            return;
        }

        // 减速和减防是持续时间型状态，每帧检查是否到期。
        // 감속과 방어력 감소는 지속시간이 있는 상태라서 매 프레임 만료 여부를 확인한다.
        if (StatusEffects != null)
        {
            StatusEffects.TickSlowState();
        }

        if (Defense != null)
        {
            Defense.Tick();
        }
    }

    public void TakeDamage(float defaultDamage, float ignoreDefenseRate)
    {
        // 受伤统一入口：外部只需要调用 EnemyAI.TakeDamage。
        // 피해 통합 입구: 외부는 EnemyAI.TakeDamage만 호출하면 된다.
        // 内部顺序是：先判断特性闪避，再计算防御减伤，最后交给血量模块扣血。
        // 내부 순서: 특성 회피 판단 -> 방어 감소 계산 -> 체력 모듈에서 체력 감소.
        if (ClassTraits != null && ClassTraits.ShouldIgnoreIncomingDamage())
        {
            return;
        }

        float finalDamage = defaultDamage;

        if (Defense != null)
        {
            finalDamage = Defense.CalculateFinalDamage(defaultDamage, ignoreDefenseRate);
        }

        if (Health != null)
        {
            Health.TakeFinalDamage(finalDamage);
        }
    }

    public void TakeDamage(float defaultDamage)
    {
        TakeDamage(defaultDamage, 0f);
    }

    public void SetSpeedDown(float slowPower, float slowTime)
    {
        // 减速入口：真正的减速数据和计时逻辑放在 EnemyStatusEffects。
        // 감속 입구: 실제 감속 데이터와 시간 처리는 EnemyStatusEffects에서 담당한다.
        if (StatusEffects == null)
        {
            return;
        }

        StatusEffects.SetSpeedDown(slowPower, slowTime);
    }

    public void AddParalyzeStack(float amount, float duration)
    {
        // 麻痹入口：外部只告诉敌人“增加多少麻痹值”和“麻痹多久”。
        // 마비 입구: 외부는 적에게 "마비 수치를 얼마나 추가할지"와 "얼마나 지속할지"만 알려준다.
        if (StatusEffects == null)
        {
            return;
        }

        StatusEffects.AddParalyzeStack(amount, duration);
    }

    public bool IsParalyzed()
    {
        if (StatusEffects == null)
        {
            return false;
        }

        return StatusEffects.IsParalyzed;
    }

    public void ReduceDefensePower(float amount, float duration)
    {
        // 固定减防入口：真正修改 defensePower 的逻辑交给 EnemyDefense。
        // 고정 방어력 감소 입구: 실제 defensePower 변경 로직은 EnemyDefense가 담당한다.
        if (Defense == null)
        {
            return;
        }

        Defense.ReduceDefensePower(amount, duration);
    }

    public float GetCurrentDamageReduction()
    {
        if (Defense == null)
        {
            return 0f;
        }

        return Defense.GetCurrentDamageReduction();
    }

    public void Dead()
    {
        // 死亡统一入口：死亡特效、自爆、销毁对象都交给 EnemyDeathHandler。
        // 사망 통합 입구: 사망 효과, 자폭, 오브젝트 제거는 EnemyDeathHandler가 담당한다.
        if (DeathHandler == null)
        {
            Destroy(gameObject);
            return;
        }

        DeathHandler.Die();
    }

    void OnDrawGizmosSelected()
    {
        EnemyAttack attackModule = AttackModule;

        if (attackModule == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackModule.attackRange);
    }

    T GetOrAddComponent<T>() where T : Component
    {
        // 泛型工具函数：先找同类型组件，找不到就自动添加。
        // 제네릭 도구 함수: 같은 타입의 컴포넌트를 먼저 찾고, 없으면 자동으로 추가한다.
        T component = GetComponent<T>();

        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}
