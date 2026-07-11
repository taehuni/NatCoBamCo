using UnityEngine;
using System.Collections.Generic;


public class SniperTower : MonoBehaviour
{
    public float damage; //伤害
    public float attackRange; //攻击范围
    public float criticalChance = 0.2f;     // 暴击率，0.2 = 20%
    public float criticalMultiplier = 2f;   // 暴击伤害倍率，2 = 造成 2 倍伤害
    public float attackInterval = 2f; //攻击间隔 / 공격 간격
    private float nextAttackTime;
    public float executeChance = 0.05f;        // 处决几率，0.05 = 5%
    public float ignoreDefenseRate = 0.2f;     // 无视防御率，0.2 = 无视 20% 减伤
    public int level = 1;
    public int maxLevel = 4;
    public float eliteDamageBonusRate = 0.2f; // 对 Elite 增伤比例，0.2 = 增伤 20%
    public float bossDamageBonusRate = 0.3f; // 对 Boss 增伤比例，0.3 = 增伤 30%
    public LayerMask enemyLayer;
    public GameObject bulletPrefab;
    public Transform firePoint;
    private List<EnemyAI> enemiesInRange = new List<EnemyAI>(); //在攻击范围内的敌人 / 공격 범위 안에 있는 적 목록

    // Update is called once per frame
    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            bool attacked = Attack();

            if (attacked)
            {
                nextAttackTime = Time.time + attackInterval;
            }
        }
    }

    bool Attack()
    {
        if (bulletPrefab == null || firePoint == null)
        {
            return false;
        }

        UpdateEnemiesInRange();

        EnemyAI targetEnemy = GetFirstEnteredEnemy();

        if (targetEnemy == null)
        {
            return false;
        }

        GameObject bulletObject = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        SniperBullet bullet = bulletObject.GetComponent<SniperBullet>();

        if (bullet != null)
        {
            bullet.Init(damage,
            criticalChance,
            criticalMultiplier,
            executeChance,
            ignoreDefenseRate,
            eliteDamageBonusRate,
            bossDamageBonusRate,
            targetEnemy);
            return true;
        }
        Destroy(bulletObject);
        return false;
    }

    void UpdateEnemiesInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (enemiesInRange[i] == null)
            {
                enemiesInRange.RemoveAt(i);
            }
            else
            {
                float distance = Vector3.Distance(transform.position, enemiesInRange[i].transform.position);
                if (distance > attackRange)
                {
                    enemiesInRange.RemoveAt(i);
                }
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyAI enemy = colliders[i].GetComponentInParent<EnemyAI>();

            if (enemy == null)
            {
                continue;
            }

            if (!enemiesInRange.Contains(enemy))
            {
                enemiesInRange.Add(enemy);
            }
        }
    }

    EnemyAI GetFirstEnteredEnemy()
    {
        if (enemiesInRange.Count == 0)
        {
            return null;
        }

        return enemiesInRange[0];
    }


    public void LevelUp()
    {
        if (level >= maxLevel)
        {
            return;
        }
        level++;
        damage += 10;
        attackRange += 5;
        attackInterval -= 0.1f;
        executeChance += 0.025f;
        ignoreDefenseRate += 0.05f;
        eliteDamageBonusRate += 0.05f;
        bossDamageBonusRate += 0.05f;
        criticalChance += 0.05f;
        criticalMultiplier += 0.25f;

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
