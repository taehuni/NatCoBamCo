using UnityEngine;

// 敌人的总入口脚本：保存敌人基础数据，并把生命、防御、状态、死亡、特性这些模块连接起来。
// 적의 메인 입구 스크립트: 기본 데이터를 보관하고 체력, 방어, 상태 이상, 사망, 특성 모듈을 연결함.
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyDefense))]
[RequireComponent(typeof(EnemyStatusEffects))]
[RequireComponent(typeof(EnemyDeathHandler))]
[RequireComponent(typeof(EnemyClassTraits))]
public class EnemyAI : MonoBehaviour
{
    [Header("Base Data / 기본 데이터")]
    public float maxHp = 30f;
    public float health = 30f;
    public float moveSpeed = 3f;
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    public float detectRange = 8f;
    public float blockedPathSearchRange = 20f;
    public float loseTargetRange = 12f;

    [Header("Defense Data / 방어 데이터")]
    public float defensePower = 10f;
    public float defenseConstant = 100f;

    [Header("Fast Enemy Trait / 고속 적 특성")]
    public float dodgeChance = 0.15f;

    [Header("Tank Enemy Trait / 탱크 적 특성")]
    public float explosionRange = 3f;
    public float explosionDamage = 20f;
    public LayerMask explosionTargetLayer;

    [Header("Enemy Grade / 적 등급")]
    public EnemyGrade enemyGrade;

    public enum EnemyGrade
    {
        Normal,
        Elite,
        Boss
    }

    [Header("Enemy Class / 적 종류")]
    public EnemyClass enemyClass;

    public enum EnemyClass
    {
        Standard,
        Fast,
        Tank,
        Ranged
    }

    [Header("Electric Tower Status Data / 전기 타워 상태 데이터")]
    public float curParalyzeStack;
    public float maxParalyzeStack = 100f;
    public float paralyzeDefensePower = 0.1f;

    private EnemyHealth enemyHealth;
    private EnemyDefense enemyDefense;
    private EnemyStatusEffects statusEffects;
    private EnemyDeathHandler deathHandler;
    private EnemyClassTraits classTraits;

    // 对外提供 EnemyHealth 的只读入口，外部可以拿到生命模块，但不能直接替换 enemyHealth 引用。
    // 외부에 EnemyHealth 읽기 전용 입구를 제공. 외부에서 체력 모듈은 가져올 수 있지만 enemyHealth 참조를 바꿀 수는 없음.
    public EnemyHealth Health
    {
        get { return enemyHealth; }
    }

    // 对外提供 EnemyDefense 的只读入口，防御力修改应通过 EnemyDefense 的函数完成。
    // 외부에 EnemyDefense 읽기 전용 입구를 제공. 방어력 변경은 EnemyDefense 함수로 처리해야 함.
    public EnemyDefense Defense
    {
        get { return enemyDefense; }
    }

    // 对外提供状态异常模块入口，例如减速、麻痹状态。
    // 감속, 마비 같은 상태 이상 모듈 입구를 제공.
    public EnemyStatusEffects StatusEffects
    {
        get { return statusEffects; }
    }

    // 对外提供死亡处理模块入口，例如坦克自爆、销毁对象。
    // 사망 처리 모듈 입구를 제공. 예: 탱크 자폭, 오브젝트 제거.
    public EnemyDeathHandler DeathHandler
    {
        get { return deathHandler; }
    }

    // 对外提供敌人类型特性模块入口，例如高速敌人闪避。
    // 적 종류 특성 모듈 입구를 제공. 예: 고속 적 회피.
    public EnemyClassTraits ClassTraits
    {
        get { return classTraits; }
    }

    void Awake()
    {
        // 获取或自动添加各个功能组件，保证敌人身上一定有这些模块。
        // 각 기능 컴포넌트를 가져오거나 자동으로 추가해서 적에게 필요한 모듈이 반드시 존재하게 함.
        enemyHealth = GetOrAddComponent<EnemyHealth>();
        enemyDefense = GetOrAddComponent<EnemyDefense>();
        statusEffects = GetOrAddComponent<EnemyStatusEffects>();
        deathHandler = GetOrAddComponent<EnemyDeathHandler>();
        classTraits = GetOrAddComponent<EnemyClassTraits>();

        // 把 EnemyAI 自己传给各模块，让模块可以读取敌人的基础数据。
        // EnemyAI 자기 자신을 각 모듈에 전달해서 모듈이 적의 기본 데이터를 읽을 수 있게 함.
        enemyHealth.Initialize(this);
        enemyDefense.Initialize(this);
        statusEffects.Initialize(this);
        deathHandler.Initialize(this);
        classTraits.Initialize(this);
    }

    void Start()
    {
        ResetRuntimeData();
    }

    // 敌人生成后初始化当前运行数据
    // 적이 생성된 뒤 현재 실행 데이터를 초기화
    public void ResetRuntimeData()
    {
        if (enemyHealth != null)
        {
            enemyHealth.ResetHealth();
        }

        if (enemyDefense != null)
        {
            enemyDefense.ResetDefenseData();
        }
    }

    void Update()
    {
        // 麻痹期间直接 return，阻止后续状态逻辑继续执行。
        // 마비 중이면 바로 return 해서 이후 상태 로직이 실행되지 않게 함.
        if (statusEffects != null && statusEffects.TickParalyzeState())
        {
            return;
        }

        // 每帧检查减速是否到期，到期后恢复速度。
        // 매 프레임 감속 시간이 끝났는지 확인하고, 끝났으면 속도를 복구함.
        if (statusEffects != null)
        {
            statusEffects.TickSlowState();
        }

        // 每帧检查防御力减少效果是否到期，到期后恢复原始防御力。
        // 매 프레임 방어력 감소 효과가 끝났는지 확인하고, 끝났으면 원래 방어력으로 복구함.
        if (enemyDefense != null)
        {
            enemyDefense.Tick();
        }
    }

    // 外部脚本统一调用这个函数造成伤害
    // 외부 스크립트는 이 함수를 통해 데미지를 줌(대미지, 방어력 무시율)
    public void TakeDamage(float defaultDamage, float ignoreDefenseRate)
    {
        // 先判断敌人特性是否让本次伤害无效，例如高速敌人的闪避。
        // 먼저 적 특성으로 이번 데미지를 무시할지 판단. 예: 고속 적의 회피.
        if (classTraits != null && classTraits.ShouldIgnoreIncomingDamage())
        {
            //대미지 회피할 때 특수 UI?Anim?Effect?
            // 闪避成功时可以在这里添加 UI、动画或特效。
            // 회피 성공 시 여기서 UI, 애니메이션, 이펙트를 추가할 수 있음.
            return;
        }

        float finalDamage = defaultDamage;

        // 如果有防御模块，就让防御模块计算最终伤害。
        // 방어 모듈이 있으면 방어 모듈이 최종 데미지를 계산함.
        if (enemyDefense != null)
        {
            finalDamage = enemyDefense.CalculateFinalDamage(defaultDamage, ignoreDefenseRate);
        }

        //Debug.Log("FinalDamage = " + finalDamage);

        if (enemyHealth != null)
        {
            enemyHealth.TakeFinalDamage(finalDamage);
        }
    }

    public void TakeDamage(float defaultDamage)
    {
        TakeDamage(defaultDamage, 0f);
    }

    // 电塔调用：减速
    // 전기 타워 호출: 감속
    public void SetSpeedDown(float slowPower, float slowTime)
    {
        if (statusEffects == null)
        {
            return;
        }

        statusEffects.SetSpeedDown(slowPower, slowTime);
    }

    // 电塔调用：麻痹层数
    // 전기 타워 호출: 마비 스택
    public void AddParalyzeStack(float amount, float duration)
    {
        if (statusEffects == null)
        {
            return;
        }

        statusEffects.AddParalyzeStack(amount, duration);
    }

    public bool IsParalyzed()
    {
        if (statusEffects == null)
        {
            return false;
        }

        return statusEffects.IsParalyzed;
    }

    // 电塔调用：固定减少 defensePower
    // 전기 타워 호출: defensePower 고정 감소
    public void ReduceDefensePower(float amount, float duration)
    {
        if (enemyDefense == null)
        {
            return;
        }

        enemyDefense.ReduceDefensePower(amount, duration);
    }

    public float GetCurrentDamageReduction()
    {
        if (enemyDefense == null)
        {
            return 0f;
        }

        return enemyDefense.GetCurrentDamageReduction();
    }

    public void Dead()
    {
        // 如果死亡模块不存在，就直接销毁，避免因为缺组件导致敌人无法消失。
        // 사망 모듈이 없으면 바로 제거해서 컴포넌트 누락 때문에 적이 사라지지 않는 문제를 방지함.
        if (deathHandler == null)
        {
            Destroy(gameObject);
            return;
        }

        deathHandler.Die();
    }

    void OnDrawGizmosSelected()
    {
        // 在 Scene 视图中显示敌人的攻击范围，方便测试。
        // Scene 뷰에서 적의 공격 범위를 표시해서 테스트하기 쉽게 함.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // 泛型工具函数：如果身上已有 T 组件就拿到它，没有就自动添加一个。
    // 제네릭 유틸 함수: T 컴포넌트가 있으면 가져오고, 없으면 자동으로 추가함.
    T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();

        // GetComponent 找不到时返回 null，这时用 AddComponent 添加脚本组件。
        // GetComponent가 찾지 못하면 null을 반환하므로 AddComponent로 스크립트 컴포넌트를 추가함.
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}
