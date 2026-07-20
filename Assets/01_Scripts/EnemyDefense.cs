using UnityEngine;

public class EnemyDefense : MonoBehaviour
{
    private EnemyAI enemyAI;

    private bool isDefenseReduced;
    private float defenseReductionEndTime;
    private float originalDefensePower;
    private float currentDefenseReduceAmount;

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

        float damageReduction = enemyAI.defensePower / (enemyAI.defensePower + enemyAI.defenseConstant);
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        return damageReduction;
    }

    // 计算最终伤害：基础伤害 -> 防御减伤 -> 无视防御 -> 最低伤害
    // 최종 데미지 계산: 기본 데미지 -> 방어 감소 -> 방어 무시 -> 최소 데미지
    public float CalculateFinalDamage(float defaultDamage, float ignoreDefenseRate)
    {
        float damageReduction = GetCurrentDamageReduction();

        damageReduction -= ignoreDefenseRate;
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        float finalDamage = defaultDamage * (1f - damageReduction);
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

        if (amount < currentDefenseReduceAmount)
        {
            return;
        }

        currentDefenseReduceAmount = amount;
        isDefenseReduced = true;
        defenseReductionEndTime = Time.time + duration;

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

        isDefenseReduced = false;
        currentDefenseReduceAmount = 0f;
        enemyAI.defensePower = originalDefensePower;
    }

    //최중 대미지 소수점 2자리 보류 保留最终伤害小数点2位
    float RoundToTwoDecimals(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }
}
