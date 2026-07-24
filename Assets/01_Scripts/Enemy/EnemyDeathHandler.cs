using System.Collections.Generic;
using UnityEngine;

public class EnemyDeathHandler : MonoBehaviour
{
    private EnemyAI enemyAI;
    private bool isDead;

    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
    }

    public void Die()
    {
        if (enemyAI == null || isDead)
        {
            return;
        }

        isDead = true;

        if (enemyAI.enemyClass == EnemyAI.EnemyClass.Tank)
        {
            Explode();
        }

        Destroy(gameObject);
    }

    // 坦克型敌人死亡自爆
    // 탱크형 적 사망 시 자폭
    void Explode()
    {
        Collider[] targets = Physics.OverlapSphere(
            transform.position,
            enemyAI.explosionRange,
            enemyAI.explosionTargetLayer
        );

        List<DamageableBuilding> damagedBuildings = new List<DamageableBuilding>();

        for (int i = 0; i < targets.Length; i++)
        {
            DamageableBuilding building = targets[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            if (damagedBuildings.Contains(building))
            {
                continue;
            }

            building.GetDamage(enemyAI.explosionDamage);
            damagedBuildings.Add(building);
        }
    }
}
