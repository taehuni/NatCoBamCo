using UnityEngine;

// 敌人类型特性模块：负责不同 enemyClass 的被动能力，例如高速敌人的闪避。
// 적 종류 특성 모듈: enemyClass별 패시브 능력을 담당. 예: 고속 적의 회피.
public class EnemyClassTraits : MonoBehaviour
{
    private EnemyAI enemyAI;

    // 保存 EnemyAI 引用，用于读取 enemyClass、dodgeChance 等数据。
    // EnemyAI 참조를 저장해서 enemyClass, dodgeChance 같은 데이터를 읽음.
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

        // 当前只有 Fast 类型有闪避能力，其他类型直接不免疫伤害。
        // 현재는 Fast 타입만 회피 능력이 있으므로, 다른 타입은 데미지를 무시하지 않음.
        if (enemyAI.enemyClass != EnemyAI.EnemyClass.Fast)
        {
            return false;
        }

        // Random.value 生成 0~1 的随机小数，小于等于 dodgeChance 就判定闪避成功。
        // Random.value는 0~1 사이의 랜덤 소수. dodgeChance 이하이면 회피 성공으로 판단.
        return Random.value <= enemyAI.dodgeChance;
    }
}
