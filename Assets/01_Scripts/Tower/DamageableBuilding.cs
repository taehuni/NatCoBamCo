using UnityEngine;
using UnityEngine.Serialization;

// 建筑兼容入口：保留旧代码使用的 DamageableBuilding 类型和函数名。
// 건물 호환 입구: 기존 코드가 사용하는 DamageableBuilding 타입과 함수 이름을 유지한다.
// 真正的血量、受伤、修理由 BuildingHealth 负责，防御计算由 BuildingDefense 负责。
// 실제 체력, 피해, 수리는 BuildingHealth가 담당하고 방어 계산은 BuildingDefense가 담당한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth))]
[RequireComponent(typeof(BuildingDefense))]
public class DamageableBuilding : MonoBehaviour
{
    // 下面四个字段只用于把旧 Prefab/Scene 中的数据安全迁移到新组件。
    // 아래 네 필드는 기존 Prefab/Scene 데이터를 새 컴포넌트로 안전하게 이전할 때만 사용한다.
    [FormerlySerializedAs("hp")]
    [SerializeField, HideInInspector] private float legacyHealth = 100f;

    [FormerlySerializedAs("maxHp")]
    [SerializeField, HideInInspector] private float legacyMaxHp = 100f;

    [FormerlySerializedAs("defensePower")]
    [SerializeField, HideInInspector] private float legacyDefensePower = 0f;

    [SerializeField, HideInInspector] private bool legacyDataMigrated;

    private BuildingHealth buildingHealth;
    private BuildingDefense buildingDefense;

    // 兼容旧代码的只读入口。数据本体已经移动到对应的新模块中。
    // 기존 코드 호환용 읽기 전용 입구다. 실제 데이터는 각각의 새 모듈에 있다.
    public float hp => Health != null ? Health.CurrentHealth : legacyHealth;
    public float maxHp => Health != null ? Health.MaxHealth : legacyMaxHp;
    public float defensePower => Defense != null ? Defense.defensePower : legacyDefensePower;

    public BuildingHealth Health
    {
        get
        {
            EnsureModulesAndMigrate();
            return buildingHealth;
        }
    }

    public BuildingDefense Defense
    {
        get
        {
            EnsureModulesAndMigrate();
            return buildingDefense;
        }
    }

    public IDamageable Damageable => Health;
    public IRepairable Repairable => Health;

    void Awake()
    {
        EnsureModulesAndMigrate();
    }

    // 新添加本组件时，RequireComponent 已经建立新模块，不需要执行旧数据迁移。
    // 이 컴포넌트를 새로 추가하면 RequireComponent가 새 모듈을 만들기 때문에 기존 데이터 이전이 필요 없다.
    void Reset()
    {
        CacheOrAddModules();
        legacyDataMigrated = true;
    }

    // 旧受伤入口：转交给实现 IDamageable 的 BuildingHealth。
    // 기존 피해 입구: IDamageable을 구현한 BuildingHealth로 전달한다.
    public void GetDamage(float damage)
    {
        IDamageable damageable = Damageable;

        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    // 旧修理入口：转交给实现 IRepairable 的 BuildingHealth。
    // 기존 수리 입구: IRepairable을 구현한 BuildingHealth로 전달한다.
    public void Repair(float healAmount)
    {
        IRepairable repairable = Repairable;

        if (repairable != null)
        {
            repairable.Repair(healAmount);
        }
    }

    void EnsureModulesAndMigrate()
    {
        CacheOrAddModules();

        if (legacyDataMigrated || buildingHealth == null || buildingDefense == null)
        {
            return;
        }

        buildingHealth.SetHealthValues(legacyMaxHp, legacyHealth);
        buildingDefense.SetDefensePower(legacyDefensePower);
        legacyDataMigrated = true;
    }

    void CacheOrAddModules()
    {
        if (buildingHealth == null)
        {
            buildingHealth = GetComponent<BuildingHealth>();

            if (buildingHealth == null)
            {
                buildingHealth = gameObject.AddComponent<BuildingHealth>();
            }
        }

        if (buildingDefense == null)
        {
            buildingDefense = GetComponent<BuildingDefense>();

            if (buildingDefense == null)
            {
                buildingDefense = gameObject.AddComponent<BuildingDefense>();
            }
        }
    }
}
