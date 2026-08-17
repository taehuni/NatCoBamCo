using UnityEngine;
using UnityEngine.Serialization;

// 建筑手动攻击点配置：按照敌人体型保存不同的站位点。
// 건물 수동 공격 지점 설정: 적의 체형에 따라 서로 다른 공격 위치를 저장한다.
// 当前规则：Standard、Fast 使用普通攻击点，Tank 使用大型攻击点，Ranged 不预约攻击点。
// 현재 규칙: Standard, Fast는 일반 공격 지점을 사용하고 Tank는 대형 공격 지점을 사용하며 Ranged는 공격 지점을 예약하지 않는다.
// 如果当前体型对应的数组没有有效点，预约系统会退回到自动计算攻击点。
// 현재 체형에 맞는 배열에 유효한 지점이 없으면 예약 시스템은 자동 공격 지점 계산으로 돌아간다.

public class BuildingAttackPoints : MonoBehaviour
{
    // 攻击点组不仅用于选择数组，也用于给预约系统区分不同体型的同号攻击点。
    // 공격 지점 그룹은 배열 선택뿐 아니라 예약 시스템에서 체형별 같은 번호의 지점을 구분할 때도 사용한다.
    public enum AttackPointGroup
    {
        Normal = 1,
        Large = 2,
        Boss = 3
    }

    [Header("일반 체형 공격 지점")]
    [FormerlySerializedAs("attackPoints")]
    public Transform[] normalAttackPoints;

    [Header("대형 체형 공격 지점")]
    public Transform[] largeAttackPoints;

    // Boss 制作完成后解除下面字段和 GetAttackPointGroupForEnemy 中 Boss 判断的注释。
    // Boss 제작이 끝나면 아래 필드와 GetAttackPointGroupForEnemy의 Boss 판단 주석을 해제한다.
    // [Header("보스 체형 공격 지점")]
    // public Transform[] bossAttackPoints;

    public Transform[] GetAttackPointsForEnemy(EnemyAI enemyAI)
    {
        // 先确定当前敌人属于哪个攻击点组，再返回对应数组。
        // 먼저 현재 적이 어느 공격 지점 그룹인지 정한 뒤 해당 배열을 반환한다.
        AttackPointGroup group = GetAttackPointGroupForEnemy(enemyAI);

        switch (group)
        {
            case AttackPointGroup.Large:
                return largeAttackPoints;

            // Boss 功能完成后解除这一段和 bossAttackPoints 字段的注释。
            // Boss 기능이 완성되면 이 부분과 bossAttackPoints 필드의 주석을 해제한다.
            // case AttackPointGroup.Boss:
            //     return bossAttackPoints;

            default:
                return normalAttackPoints;
        }
    }

    public static AttackPointGroup GetAttackPointGroupForEnemy(EnemyAI enemyAI)
    {
        // 没有敌人类型数据时使用普通组，保证基础逻辑仍然可以运行。
        // 적 타입 데이터가 없으면 일반 그룹을 사용해서 기본 로직이 계속 동작하게 한다.
        if (enemyAI == null || enemyAI.ClassTraits == null)
        {
            return AttackPointGroup.Normal;
        }

        // Boss 制作完成后解除这里的注释，Boss 会优先于普通敌人种类判断。
        // Boss 제작이 끝나면 이 주석을 해제한다. Boss 판정은 일반 적 종류 판정보다 먼저 처리된다.
        // if (enemyAI.ClassTraits.enemyGrade == EnemyAI.EnemyGrade.Boss)
        // {
        //     return AttackPointGroup.Boss;
        // }

        // Tank 体型较大，所以使用大型攻击点；Ranged 会在行为层跳过整个预约系统。
        // Tank는 체형이 크므로 대형 공격 지점을 사용하고, Ranged는 행동 계층에서 예약 시스템 전체를 건너뛴다.
        if (enemyAI.ClassTraits.enemyClass == EnemyAI.EnemyClass.Tank)
        {
            return AttackPointGroup.Large;
        }

        return AttackPointGroup.Normal;
    }
}
