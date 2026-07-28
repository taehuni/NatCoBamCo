using UnityEngine;

// 敌人血量模块：只负责最大血量、当前血量、扣血、死亡判断。
// 적 체력 모듈: 최대 체력, 현재 체력, 체력 감소, 사망 판단만 담당한다.
// 注意：这里接收的是已经计算好的最终伤害，不负责防御、暴击、闪避等规则。
// 주의: 여기서는 계산이 끝난 최종 피해만 받는다. 방어, 치명타, 회피 규칙은 담당하지 않는다.

// 生命值模块：只负责最大生命、当前生命、扣血、死亡判断。
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Data / 체력 데이터")]
    public float maxHp;
    public float health;

    private EnemyAI enemyAI;

    public void Initialize(EnemyAI enemyAI)
    {
        // 缓存 EnemyAI 入口，死亡时可以回到 EnemyAI.Dead() 统一处理死亡逻辑。
        // EnemyAI 입구를 캐시한다. 사망 시 EnemyAI.Dead()로 돌아가 통합 사망 처리를 하기 위해서다.
        this.enemyAI = enemyAI;
    }

    public void ResetHealth()
    {
        // 新生成或者重置时，把当前血量恢复到最大血量。
        // 새로 생성되거나 초기화될 때 현재 체력을 최대 체력으로 맞춘다.
        health = maxHp;
    }

    public void TakeFinalDamage(float finalDamage)
    {
        // 这里的 finalDamage 已经是经过防御、无视防御等计算后的最终伤害。
        // finalDamage는 방어력, 방어 무시 등을 거친 최종 피해량이다.
        health -= finalDamage;

        // 血量小于等于 0 就进入死亡流程。
        // 체력이 0 이하가 되면 사망 처리로 들어간다.
        if (health <= 0f)
        {
            if (enemyAI != null)
            {
                // 正常情况交给 EnemyAI，再由 EnemyDeathHandler 处理自爆、销毁等逻辑。
                // 정상 상황에서는 EnemyAI로 넘기고, EnemyDeathHandler가 자폭과 제거를 처리한다.
                enemyAI.Dead();
            }
            else
            {
                // 兜底处理：如果没有 EnemyAI，就至少销毁自己，避免死掉的敌人留在场景中。
                // 예외 처리: EnemyAI가 없으면 최소한 자기 자신을 제거해서 죽은 적이 씬에 남지 않게 한다.
                Destroy(gameObject);
            }
        }
    }
}
