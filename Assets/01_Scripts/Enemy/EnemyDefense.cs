using UnityEngine;

// 敌人防御模块：负责防御力、减伤公式、无视防御、固定减防 Debuff。
// 적 방어 모듈: 방어력, 피해 감소 공식, 방어 무시, 고정 방어력 감소 디버프를 담당한다.
// 它只负责“伤害应该被减到多少”，不直接扣血。
// 이 모듈은 "피해가 얼마로 줄어드는지"만 계산하고, 체력은 직접 깎지 않는다.

// 防御模块：负责防御力、减伤公式、无视防御、固定减防 Debuff。
public class EnemyDefense : MonoBehaviour
{
    [Header("Defense Data / 방어 데이터")]
    public float defensePower;
    public float defenseConstant;

    private bool isDefenseReduced;
    private float defenseReductionEndTime;
    private float originalDefensePower;
    private float currentDefenseReduceAmount;

    public void Initialize(EnemyAI enemyAI)
    {
        // 目前防御模块不需要反向引用 EnemyAI，保留入口是为了以后扩展。
        // 현재 방어 모듈은 EnemyAI 참조가 필요 없지만, 이후 확장을 위해 입구를 남겨둔다.
    }

    public void ResetDefenseData()
    {
        // 记录初始防御力。之后减防结束时，会恢复到这个值。
        // 초기 방어력을 기록한다. 방어력 감소가 끝나면 이 값으로 되돌린다.
        originalDefensePower = defensePower;
        // 当前正在生效的减防强度，0 表示没有减防。
        // 현재 적용 중인 방어력 감소량이다. 0이면 감소 효과가 없다.
        currentDefenseReduceAmount = 0f;
        isDefenseReduced = false;
        defenseReductionEndTime = 0f;
    }

    public void Tick()
    {
        // 每帧检查固定减防 Debuff 是否到期。
        // 매 프레임 고정 방어력 감소 디버프가 끝났는지 확인한다.
        HandleDefenseReductionState();
    }

    public float GetCurrentDamageReduction()
    {
        // 防御力或者防御常数不合法时，直接认为没有减伤。
        // 방어력 또는 방어 상수가 유효하지 않으면 피해 감소가 없다고 본다.
        if (defensePower <= 0f || defenseConstant <= 0f)
        {
            return 0f;
        }

        // 减伤公式：防御力越高，减伤越高，但增长会逐渐变慢。
        // 피해 감소 공식: 방어력이 높을수록 감소율이 높지만, 증가 속도는 점점 느려진다.
        float damageReduction = defensePower / (defensePower + defenseConstant);
        // 限制在 0% 到 90% 之间，避免出现负减伤或者完全打不动。
        // 0%~90% 사이로 제한해서 음수 감소율이나 거의 무적 상태를 막는다.
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        return damageReduction;
    }

    public float CalculateFinalDamage(float defaultDamage, float ignoreDefenseRate)
    {
        // 先根据当前 defensePower 算出基础减伤比例。
        // 먼저 현재 defensePower를 기준으로 기본 피해 감소율을 계산한다.
        float damageReduction = GetCurrentDamageReduction();

        // ignoreDefenseRate 是“无视防御比例”，例如 0.3 就是少算 30% 减伤。
        // ignoreDefenseRate는 "방어 무시 비율"이다. 예: 0.3이면 피해 감소율을 30% 덜 적용한다.
        damageReduction -= ignoreDefenseRate;
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        // 最终伤害 = 原始伤害 * 没有被减掉的比例。
        // 최종 피해 = 원래 피해 * 감소되지 않은 비율.
        float finalDamage = defaultDamage * (1f - damageReduction);
        // 保底 1 点伤害，避免防御太高时完全不掉血。
        // 최소 1 피해를 보장해서 방어력이 높아도 완전히 피해가 0이 되지 않게 한다.
        finalDamage = Mathf.Max(finalDamage, 1f);

        return RoundToTwoDecimals(finalDamage);
    }

    public void ReduceDefensePower(float amount, float duration)
    {
        // 如果新减防比当前减防弱，就不覆盖，避免低级效果刷新高级效果。
        // 새 방어 감소 효과가 현재 효과보다 약하면 덮어쓰지 않는다.
        if (amount < currentDefenseReduceAmount)
        {
            return;
        }

        // amount 是百分比，例如 0.2 表示把防御力降低 20%。
        // amount는 비율이다. 예: 0.2는 방어력을 20% 낮춘다는 뜻이다.
        currentDefenseReduceAmount = amount;
        isDefenseReduced = true;
        defenseReductionEndTime = Time.time + duration;

        // 固定减防：直接修改当前 defensePower。
        // 고정 방어력 감소: 현재 defensePower 값을 직접 낮춘다.
        defensePower = originalDefensePower * (1f - amount);
    }

    void HandleDefenseReductionState()
    {
        // 没有减防状态时不需要继续判断。
        // 방어력 감소 상태가 아니면 더 이상 검사하지 않는다.
        if (!isDefenseReduced)
        {
            return;
        }

        // 时间还没到，减防继续生效。
        // 시간이 아직 끝나지 않았으면 방어력 감소가 계속 유지된다.
        if (Time.time < defenseReductionEndTime)
        {
            return;
        }

        // 时间结束，清空减防状态，并恢复原始防御力。
        // 시간이 끝나면 감소 상태를 초기화하고 원래 방어력으로 복구한다.
        isDefenseReduced = false;
        currentDefenseReduceAmount = 0f;
        defensePower = originalDefensePower;
    }

    float RoundToTwoDecimals(float value)
    {
        // 先放大 100 倍再四舍五入，最后除回去，就能保留两位小数。
        // 100배 키운 뒤 반올림하고 다시 나누면 소수 둘째 자리까지 남길 수 있다.
        return Mathf.Round(value * 100f) / 100f;
    }
}
