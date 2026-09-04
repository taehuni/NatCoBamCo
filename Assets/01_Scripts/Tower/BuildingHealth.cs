using UnityEngine;

// 建筑血量模块：保存最大血量和当前血量，并提供受伤、修理两种能力。
// 건물 체력 모듈: 최대 체력과 현재 체력을 저장하고 피해 및 수리 능력을 제공한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingDefense))]
public class BuildingHealth : MonoBehaviour, IDamageable, IRepairable
{
    [Header("건물 체력 데이터")]
    [Min(0f)] public float maxHp = 100f;
    [Min(0f)] public float health = 100f;

    private BuildingDefense buildingDefense;
    private bool isDestroyed;

    public float CurrentHealth => health;
    public float MaxHealth => maxHp;
    public bool IsAlive => !isDestroyed && health > 0f;
    public bool NeedsRepair => IsAlive && health < maxHp;

    void Awake()
    {
        CacheComponents();
        ClampHealth();
    }

    void OnValidate()
    {
        ClampHealth();
    }

    // IDamageable 的实现：先让防御模块计算最终伤害，再扣除建筑血量。
    // IDamageable 구현: 방어 모듈에서 최종 피해를 계산한 뒤 건물 체력을 감소시킨다.
    public void TakeDamage(float damage)
    {
        if (isDestroyed || health <= 0f)
        {
            return;
        }

        CacheComponents();

        float finalDamage = buildingDefense != null
            ? buildingDefense.CalculateFinalDamage(damage)
            : damage;

        health -= finalDamage;

        if (health <= 0f)
        {
            health = 0f;
            isDestroyed = true;
            Destroy(gameObject);
        }
    }

    // IRepairable 的实现：只修理仍然存在且尚未满血的建筑。
    // IRepairable 구현: 아직 존재하고 최대 체력이 아닌 건물만 수리한다.
    public void Repair(float repairAmount)
    {
        if (!IsAlive || repairAmount <= 0f || health >= maxHp)
        {
            return;
        }

        health = Mathf.Min(health + repairAmount, maxHp);
    }

    // 把旧 DamageableBuilding 中的血量数据迁移到新的血量模块。
    // 기존 DamageableBuilding의 체력 데이터를 새 체력 모듈로 이전한다.
    public void SetHealthValues(float newMaxHp, float newHealth)
    {
        maxHp = Mathf.Max(0f, newMaxHp);
        health = Mathf.Clamp(newHealth, 0f, maxHp);
        isDestroyed = health <= 0f;
    }

    void CacheComponents()
    {
        if (buildingDefense == null)
        {
            buildingDefense = GetComponent<BuildingDefense>();
        }
    }

    void ClampHealth()
    {
        maxHp = Mathf.Max(0f, maxHp);
        health = Mathf.Clamp(health, 0f, maxHp);
    }
}
