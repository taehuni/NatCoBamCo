using UnityEngine;

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

    public EnemyHealth Health
    {
        get { return enemyHealth; }
    }

    public EnemyDefense Defense
    {
        get { return enemyDefense; }
    }

    public EnemyStatusEffects StatusEffects
    {
        get { return statusEffects; }
    }

    public EnemyDeathHandler DeathHandler
    {
        get { return deathHandler; }
    }

    public EnemyClassTraits ClassTraits
    {
        get { return classTraits; }
    }

    void Awake()
    {
        enemyHealth = GetOrAddComponent<EnemyHealth>();
        enemyDefense = GetOrAddComponent<EnemyDefense>();
        statusEffects = GetOrAddComponent<EnemyStatusEffects>();
        deathHandler = GetOrAddComponent<EnemyDeathHandler>();
        classTraits = GetOrAddComponent<EnemyClassTraits>();

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
        if (statusEffects != null && statusEffects.TickParalyzeState())
        {
            return;
        }

        if (statusEffects != null)
        {
            statusEffects.TickSlowState();
        }

        if (enemyDefense != null)
        {
            enemyDefense.Tick();
        }
    }

    // 外部脚本统一调用这个函数造成伤害
    // 외부 스크립트는 이 함수를 통해 데미지를 줌(대미지, 방어력 무시율)
    public void TakeDamage(float defaultDamage, float ignoreDefenseRate)
    {
        if (classTraits != null && classTraits.ShouldIgnoreIncomingDamage())
        {
            //대미지 회피할 때 특수 UI?Anim?Effect?
            return;
        }

        float finalDamage = defaultDamage;

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
        if (deathHandler == null)
        {
            Destroy(gameObject);
            return;
        }

        deathHandler.Die();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();

        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}
