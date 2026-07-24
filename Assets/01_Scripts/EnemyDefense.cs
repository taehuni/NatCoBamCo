using UnityEngine;

// 防御模块：负责防御减伤公式、无视防御、固定减防 Debuff、恢复防御力。
// 방어 모듈: 방어 감소 공식, 방어 무시, 고정 방어력 감소 디버프, 방어력 복구를 담당함.
public class EnemyDefense : MonoBehaviour
{
    private EnemyAI enemyAI;

    // 当前是否处于防御力被减少的状态。
    // 현재 방어력이 감소된 상태인지 여부.
    private bool isDefenseReduced;
    // 防御力减少效果结束的时间点。
    // 방어력 감소 효과가 끝나는 시간.
    private float defenseReductionEndTime;
    // 出生/初始化时记录的原始防御力，用于 Debuff 结束后恢复。
    // 생성/초기화 시 기록한 원래 방어력. 디버프 종료 후 복구에 사용.
    private float originalDefensePower;
    // 当前正在生效的减防强度，用来判断新的弱减防是否应该被忽略。
    // 현재 적용 중인 방어력 감소 강도. 더 약한 새 디버프를 무시할 때 사용.
    private float currentDefenseReduceAmount;

    // 保存 EnemyAI 引用，防御模块通过它读取和修改 defensePower 等基础数据。
    // EnemyAI 참조를 저장하고, 이 모듈은 이를 통해 defensePower 같은 기본 데이터를 읽고 수정함.
    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
    }

    // 出生时记录原始防御力
    // 생성될 때 원래 방어력을 기록
    public void ResetDefenseData()
    {
        if (enemyAI == null)
        {
            return;
        }

        // 记录原始防御力，防御力 debuff 结束后会恢复到这个值
        // 원래 방어력을 저장하고, 방어력 감소가 끝나면 이 값으로 복구
        originalDefensePower = enemyAI.defensePower;
        currentDefenseReduceAmount = 0f;
        isDefenseReduced = false;
        defenseReductionEndTime = 0f;
    }

    public void Tick()
    {
        // 每帧检查减防 Debuff 是否到期。
        // 매 프레임 방어력 감소 디버프가 끝났는지 확인함.
        HandleDefenseReductionState();
    }

    // 根据当前防御力计算减伤比例
    // 현재 방어력으로 피해 감소율 계산
    public float GetCurrentDamageReduction()
    {
        if (enemyAI == null)
        {
            return 0f;
        }

        // 防御公式：防御越高减伤越高，但增长会越来越慢。
        // 방어 공식: 방어력이 높을수록 피해 감소율이 높지만, 증가 속도는 점점 느려짐.
        float damageReduction = enemyAI.defensePower / (enemyAI.defensePower + enemyAI.defenseConstant);
        // 限制减伤比例，最低 0%，最高 90%，避免敌人完全不掉血。
        // 피해 감소율을 0%~90%로 제한해서 적이 완전히 데미지를 안 받는 상황을 방지함.
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        return damageReduction;
    }

    // 计算最终伤害：基础伤害 -> 防御减伤 -> 无视防御 -> 最低伤害
    // 최종 데미지 계산: 기본 데미지 -> 방어 감소 -> 방어 무시 -> 최소 데미지
    public float CalculateFinalDamage(float defaultDamage, float ignoreDefenseRate)
    {
        float damageReduction = GetCurrentDamageReduction();

        // 无视防御率直接减少当前减伤比例，例如 0.3 表示少算 30% 减伤。
        // 방어 무시율은 현재 피해 감소율을 직접 줄임. 예: 0.3이면 피해 감소율을 30% 줄임.
        damageReduction -= ignoreDefenseRate;
        // 再次限制范围，避免无视防御后变成负减伤或超过上限。
        // 방어 무시 적용 후에도 범위를 제한해서 음수 감소율이나 상한 초과를 방지함.
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        // 最终伤害 = 原始伤害 * 没有被减掉的比例。
        // 최종 데미지 = 기본 데미지 * 감소되지 않은 비율.
        float finalDamage = defaultDamage * (1f - damageReduction);
        // 最低伤害保护，避免防御太高导致完全不掉血。
        // 최소 데미지 보정. 방어력이 너무 높아도 완전히 0 데미지가 되지 않게 함.
        finalDamage = Mathf.Max(finalDamage, 1f);

        return RoundToTwoDecimals(finalDamage);
    }

    // 固定减少 defensePower 的比例，例如 amount = 0.2 表示防御力变成 80%
    // defensePower를 비율로 감소시킴. 예: amount = 0.2면 방어력이 80%가 됨
    public void ReduceDefensePower(float amount, float duration)
    {
        if (enemyAI == null)
        {
            return;
        }

        // 如果新减防比当前减防弱，就不覆盖，避免弱效果刷新强效果。
        // 새 방어력 감소가 현재 효과보다 약하면 덮어쓰지 않음. 약한 효과가 강한 효과를 갱신하는 것을 방지.
        if (amount < currentDefenseReduceAmount)
        {
            return;
        }

        // 记录当前减防强度和结束时间。
        // 현재 방어력 감소 강도와 종료 시간을 기록.
        currentDefenseReduceAmount = amount;
        isDefenseReduced = true;
        defenseReductionEndTime = Time.time + duration;

        // 固定按原始防御力计算，避免多次减防在当前防御力基础上反复叠乘。
        // 현재 방어력이 아니라 원래 방어력을 기준으로 계산해서 여러 번 적용될 때 계속 중첩 곱셈되는 것을 방지.
        enemyAI.defensePower = originalDefensePower * (1f - amount);
    }

    void HandleDefenseReductionState()
    {
        if (enemyAI == null || !isDefenseReduced)
        {
            return;
        }

        if (Time.time < defenseReductionEndTime)
        {
            return;
        }

        // 持续时间结束，清空状态并恢复原始防御力。
        // 지속 시간이 끝나면 상태를 초기화하고 원래 방어력으로 복구.
        isDefenseReduced = false;
        currentDefenseReduceAmount = 0f;
        enemyAI.defensePower = originalDefensePower;
    }

    // 保留最终伤害小数点 2 位。
    // 최종 데미지를 소수점 둘째 자리까지 반올림.
    float RoundToTwoDecimals(float value)
    {
        // 先乘 100，再四舍五入，再除以 100。
        // 먼저 100을 곱하고 반올림한 뒤 다시 100으로 나눔.
        return Mathf.Round(value * 100f) / 100f;
    }
}
