using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    private EnemyAI enemyAI;

    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
    }

    // 出生时恢复到最大生命值
    // 생성될 때 최대 체력으로 초기화
    public void ResetHealth()
    {
        if (enemyAI == null)
        {
            return;
        }

        enemyAI.health = enemyAI.maxHp;
    }

    // 受到已经计算完防御后的最终伤害
    // 방어력 계산이 끝난 최종 데미지를 받음
    public void TakeFinalDamage(float finalDamage)
    {
        if (enemyAI == null)
        {
            return;
        }

        enemyAI.health -= finalDamage;

        if (enemyAI.health <= 0f)
        {
            enemyAI.Dead();
        }
    }
}
