using UnityEngine;

// 生命值模块：只负责生命初始化、扣血、判断死亡。
// 체력 모듈: 체력 초기화, 체력 감소, 사망 판단만 담당함.
public class EnemyHealth : MonoBehaviour
{
    private EnemyAI enemyAI;

    // 初始化时保存 EnemyAI 引用，后面通过它读取 maxHp、health，并调用 Dead。
    // 초기화할 때 EnemyAI 참조를 저장하고, 이후 maxHp, health를 읽거나 Dead를 호출함.
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

        // 这里传进来的 finalDamage 已经完成防御力、无视防御等计算。
        // 여기로 들어온 finalDamage는 이미 방어력, 방어 무시 계산이 끝난 값.
        enemyAI.health -= finalDamage;

        // 生命值小于等于 0 时，交给 EnemyAI 的死亡入口处理。
        // 체력이 0 이하가 되면 EnemyAI의 사망 입구로 처리함.
        if (enemyAI.health <= 0f)
        {
            enemyAI.Dead();
        }
    }
}
