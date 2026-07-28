using UnityEngine;

// 敌人类型特性模块：保存敌人等级、敌人种类，以及不同种类的被动特性。
// 적 타입 특성 모듈: 적 등급, 적 종류, 그리고 종류별 패시브 특성을 저장한다.
// 目前只实现了 Fast 敌人的闪避，以后 Boss 技能、Tank 特性也可以拆到这里或单独脚本。
// 현재는 Fast 적의 회피만 구현되어 있다. 이후 Boss 스킬, Tank 특성은 여기 또는 별도 스크립트로 분리할 수 있다.

// 敌人类型/等级/特性模块：负责敌人分类，以及对应被动特性。
public class EnemyClassTraits : MonoBehaviour
{
    [Header("Enemy Grade / 적 등급")]
    public EnemyAI.EnemyGrade enemyGrade;

    [Header("Enemy Class / 적 종류")]
    public EnemyAI.EnemyClass enemyClass;

    [Header("Fast Enemy Trait / 고속 적 특성")]
    public float dodgeChance;

    public void Initialize(EnemyAI enemyAI)
    {
        // 当前模块暂时不需要初始化数据，保留入口是为了以后扩展。
        // 현재 모듈은 아직 초기화 데이터가 필요 없지만, 이후 확장을 위해 입구를 남겨둔다.
    }

    public bool ShouldIgnoreIncomingDamage()
    {
        // 只有 Fast 类型敌人才有闪避逻辑。
        // Fast 타입 적만 회피 로직을 가진다.
        if (enemyClass != EnemyAI.EnemyClass.Fast)
        {
            return false;
        }

        // Random.value 会返回 0~1 的随机小数。
        // Random.value는 0~1 사이의 랜덤 소수를 반환한다.
        // dodgeChance = 0.15 时，随机数小于等于 0.15 就算闪避成功。
        // dodgeChance가 0.15이면 랜덤 값이 0.15 이하일 때 회피 성공으로 본다.
        return Random.value <= dodgeChance;
    }
}
