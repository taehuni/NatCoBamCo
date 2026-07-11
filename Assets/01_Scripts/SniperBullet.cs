using UnityEngine;

public class SniperBullet : MonoBehaviour
{
    private float damage; //伤害
    private float criticalChance;     // 暴击率，0.2 = 20%
    private float criticalMultiplier;   // 暴击伤害倍率，2 = 造成 2 倍伤害
    private float executeChance;        // 处决几率，0.05 = 5%
    private float ignoreDefenseRate;     // 无视防御率，0.2 = 无视 20% 减伤
    private float eliteDamageBonusRate; // 对 Elite 增伤比例，0.2 = 增伤 20%
    private float bossDamageBonusRate; // 对 Boss 增伤比例，0.3 = 增伤 30%
    private EnemyAI targetEnemy;
    private bool hasHit;
    public float hitDistance = 0.2f;
    public float moveSpeed = 20f;


    // Update is called once per frame
    void Update()
    {
        if (targetEnemy == null)
        {
            Destroy(gameObject);
            return;
        }
        FlyAtEnemy();
        CheckHit();
    }

    void HitTarget()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        float finalDamage = damage;

        if (targetEnemy.enemyType == EnemyAI.EnemyType.Elite)
        {
            finalDamage *= (1 + eliteDamageBonusRate);
        }

        if (targetEnemy.enemyType == EnemyAI.EnemyType.Boss)
        {
            finalDamage *= (1 + bossDamageBonusRate);
        }

        if (targetEnemy.enemyType == EnemyAI.EnemyType.Normal)
        {
            if (Random.value <= executeChance)
            {
                // 처지시 특수 한 effect 등 추가
                Debug.Log(targetEnemy.name + "를 처형했다");
                targetEnemy.Dead();
                Destroy(gameObject);
                return;
            }
        }

        if (Random.value <= criticalChance)
        {
            finalDamage *= criticalMultiplier;
        }

        targetEnemy.TakeDamage(finalDamage, ignoreDefenseRate);
    }

    public void FlyAtEnemy()
    {
        transform.position = Vector3.MoveTowards(transform.position,
        targetEnemy.transform.position,
        moveSpeed * Time.deltaTime);
    }

    public void Init(float towerDamage,
     float towerCriticalChance,
     float towerCriticalMultiplier,
     float towerExecuteChance,
     float towerIgnoreDefenseRate,
     float towerEliteDamageBonusRate,
     float towerBossDamageBonusRate,
     EnemyAI towerTargetEnemy)
    {
        damage = towerDamage;
        criticalChance = towerCriticalChance;
        criticalMultiplier = towerCriticalMultiplier;
        executeChance = towerExecuteChance;
        ignoreDefenseRate = towerIgnoreDefenseRate;
        eliteDamageBonusRate = towerEliteDamageBonusRate;
        bossDamageBonusRate = towerBossDamageBonusRate;
        targetEnemy = towerTargetEnemy;
    }

    //距离检测
    //거리 기반 명중 검사
    void CheckHit()
    {
        float distance = Vector3.Distance(transform.position, targetEnemy.transform.position);
        if (distance <= hitDistance)
        {
            HitTarget();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();

        if (enemy == null)
        {
            return;
        }

        if (enemy == targetEnemy)
        {
            HitTarget();
            Destroy(gameObject);
        }
    }
}
