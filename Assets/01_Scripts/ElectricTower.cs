using UnityEngine;
using System.Collections.Generic;

public class ElectricTower : MonoBehaviour
{
    //public Collider[] enemies;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float attackRange = 10f;
    public float attackInterval = 0.9f;
    public int damage = 2;
    public LayerMask enemyLayer;
    public float coldTime = 2f;
    public float coldPower = 0.3f;
    public float maxColdPower = 0.6f;
    public int maxTargets = 3;
    public int level = 1;
    public int maxlevel = 4;
    public float paralyzeDuration = 2f; //마비 지속시간
    public int paralyzeStackIncrease = 10; //마비 스택 증가량
    private float nextAttackTime;
    private List<EnemyAI> enemiesInRange = new List<EnemyAI>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            Attack();

            nextAttackTime = Time.time + attackInterval;
        }
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

    void Attack()
    {
        if (bulletPrefab == null || firePoint == null)
        {
            return;
        }

        UpdateEnemiesInRange();

        EnemyAI targetEnemy = GetFirstEnteredEnemy();
        //없으면 동작이 안해

        if (targetEnemy == null)
        {
            return;
        }

        GameObject bulletObject = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        ElectricBullet bullet = bulletObject.GetComponent<ElectricBullet>();
        
        if (bullet != null)
        {
            bullet.Init(
                targetEnemy,
                damage,
                coldPower,
                coldTime,
                paralyzeStackIncrease,
                paralyzeDuration
            );
        }

    }

    public void LevelUp()
    {
        if (level >= maxlevel)
        {
            return;
        }
        level++;
        damage += 2;
        attackRange += 2f;
        attackInterval -= 0.1f;
        coldTime += 0.5f;
        coldPower += 0.05f;
        coldPower = Mathf.Clamp(coldPower, 0f, maxColdPower);
        if (level % 2 == 0)
        {
            maxTargets++;
        }
        paralyzeDuration += 0.2f;
        paralyzeStackIncrease += 5;
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
