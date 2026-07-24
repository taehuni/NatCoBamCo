using UnityEngine;

public class EnemyClassTraits : MonoBehaviour
{
    private EnemyAI enemyAI;

    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
    }

    // 根据敌人类型判断这次伤害是否被特殊能力免疫
    // 적 종류에 따라 이번 데미지를 특수 능력으로 회피할지 판단
    public bool ShouldIgnoreIncomingDamage()
    {
        if (enemyAI == null)
        {
            return false;
        }

        if (enemyAI.enemyClass != EnemyAI.EnemyClass.Fast)
        {
            return false;
        }

        return Random.value <= enemyAI.dodgeChance;
    }
}
