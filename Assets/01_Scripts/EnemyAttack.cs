using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    private EnemyAI enemyAI;
    private EnemyMovement movement;
    private float nextAttackTime;

    public void Initialize(EnemyAI enemyAI, EnemyMovement movement)
    {
        this.enemyAI = enemyAI;
        this.movement = movement;
    }

    public void AttackBuilding(DamageableBuilding building)
    {
        if (building == null || enemyAI == null)
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

        building.GetDamage(enemyAI.attackDamage);
        Debug.Log(gameObject.name + " attacked " + building.gameObject.name + " for " + enemyAI.attackDamage + " damage");

        nextAttackTime = Time.time + enemyAI.attackCooldown;
    }

    public void AttackCore(Core core)
    {
        if (core == null || enemyAI == null)
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

        core.GetDamage(enemyAI.attackDamage);
        Debug.Log(gameObject.name + " attacked " + core.gameObject.name + " for " + enemyAI.attackDamage + " damage");

        nextAttackTime = Time.time + enemyAI.attackCooldown;
    }
}
