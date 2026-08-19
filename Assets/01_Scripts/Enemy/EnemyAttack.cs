using UnityEngine;

// 敌人攻击模块：负责攻击距离、攻击力、攻击冷却，以及真正对建筑/Core造成伤害。
// 적 공격 모듈: 공격 거리, 공격력, 공격 쿨다운, 건물/Core에 실제 피해를 주는 처리를 담당한다.
// 它不负责找目标，也不负责移动到目标旁边；这些由其他模块处理。
// 이 모듈은 타깃 탐색이나 타깃 근처로 이동하는 것은 담당하지 않는다. 그 부분은 다른 모듈이 처리한다.

// 攻击模块：负责攻击距离、攻击力、攻击冷却，以及真正扣建筑/Core血。
public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Data / 공격 데이터")]
    public float attackRange;
    public float attackDamage;
    public float attackCooldown;

    [Header("Ranged Attack Data / 원거리 공격 데이터")]
    public Transform firePos;
    public EnemyRangedProjectile rangedProjectilePrefab;

    private EnemyMovement movement;
    private float nextAttackTime;
    private bool hasWarnedMissingRangedSetup;

    public void Initialize(EnemyAI enemyAI, EnemyMovement movement)
    {
        // 缓存移动模块。攻击时会先停止移动，避免边走边攻击导致位置判断混乱。
        // 이동 모듈을 캐시한다. 공격할 때 먼저 멈춰서 이동 중 공격으로 인한 위치 판정 혼란을 줄인다.
        this.movement = movement;
    }

    public void AttackBuilding(DamageableBuilding building)
    {
        // 没有建筑目标时直接返回。
        // 건물 타깃이 없으면 바로 반환한다.
        if (building == null)
        {
            return;
        }

        // 攻击前停止移动。
        // 공격하기 전에 이동을 멈춘다.
        if (movement != null)
        {
            movement.Stop();
        }

        // 冷却没到，不允许攻击。
        // 쿨다운이 끝나지 않았으면 공격하지 않는다.
        if (Time.time < nextAttackTime)
        {
            return;
        }

        // 对建筑造成伤害。
        // 건물에 피해를 준다.
        building.GetDamage(attackDamage);
        Debug.Log(gameObject.name + " attacked " + building.gameObject.name + " for " + attackDamage + " damage");

        // 记录下一次允许攻击的时间。
        // 다음 공격이 가능한 시간을 기록한다.
        nextAttackTime = Time.time + attackCooldown;
    }

    // 远程攻击建筑：在 FirePos 生成投射物，伤害在投射物抵达目标后结算。
    // 원거리 건물 공격: FirePos에서 투사체를 생성하고, 투사체가 목표에 도착했을 때 피해를 적용한다.
    public void AttackRangedBuilding(DamageableBuilding building)
    {
        if (building == null)
        {
            return;
        }

        if (movement != null)
        {
            movement.Stop();
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        if (!CanSpawnRangedProjectile())
        {
            return;
        }

        EnemyRangedProjectile projectile = Instantiate(
            rangedProjectilePrefab,
            firePos.position,
            firePos.rotation
        );

        projectile.Initialize(building, attackDamage);
        nextAttackTime = Time.time + attackCooldown;
    }

    public void AttackCore(Core core)
    {
        // 没有 Core 时直接返回。
        // Core가 없으면 바로 반환한다.
        if (core == null)
        {
            return;
        }

        // 攻击 Core 前也停止移动。
        // Core를 공격하기 전에도 이동을 멈춘다.
        if (movement != null)
        {
            movement.Stop();
        }

        // 共用同一个攻击冷却，避免同一只敌人同时狂打多个目标。
        // 같은 공격 쿨다운을 사용해서 한 적이 여러 타깃을 동시에 계속 공격하지 못하게 한다.
        if (Time.time < nextAttackTime)
        {
            return;
        }

        // 对 Core 造成伤害。
        // Core에 피해를 준다.
        core.GetDamage(attackDamage);
        Debug.Log(gameObject.name + " attacked " + core.gameObject.name + " for " + attackDamage + " damage");

        // 更新下一次攻击时间。
        // 다음 공격 시간을 갱신한다.
        nextAttackTime = Time.time + attackCooldown;
    }

    // 远程攻击 Core：和攻击建筑共用同一个投射物预制体与攻击冷却。
    // 원거리 Core 공격: 건물 공격과 같은 투사체 프리팹 및 공격 쿨다운을 사용한다.
    public void AttackRangedCore(Core core)
    {
        if (core == null)
        {
            return;
        }

        if (movement != null)
        {
            movement.Stop();
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        if (!CanSpawnRangedProjectile())
        {
            return;
        }

        EnemyRangedProjectile projectile = Instantiate(
            rangedProjectilePrefab,
            firePos.position,
            firePos.rotation
        );

        projectile.Initialize(core, attackDamage);
        nextAttackTime = Time.time + attackCooldown;
    }

    // 检查远程攻击需要的引用是否已经在 Inspector 中设置。
    // 원거리 공격에 필요한 참조가 Inspector에 설정되어 있는지 확인한다.
    bool CanSpawnRangedProjectile()
    {
        if (firePos != null && rangedProjectilePrefab != null)
        {
            hasWarnedMissingRangedSetup = false;
            return true;
        }

        // Update 会反复尝试攻击，所以只提示一次，避免 Console 被同一条警告刷满。
        // Update에서 공격을 반복 시도하므로 같은 경고가 Console을 가득 채우지 않도록 한 번만 출력한다.
        if (!hasWarnedMissingRangedSetup)
        {
            Debug.LogWarning(
                gameObject.name +
                " cannot perform ranged attack. Assign Fire Pos and Ranged Projectile Prefab on EnemyAttack."
            );
            hasWarnedMissingRangedSetup = true;
        }

        return false;
    }
}
