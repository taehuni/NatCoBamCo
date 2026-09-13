using UnityEngine;

// 建筑防御模块：只保存防御数据，并负责把原始伤害换算成最终伤害。
// 건물 방어 모듈: 방어 데이터를 저장하고 원래 피해를 최종 피해로 계산하는 역할만 담당한다.
[DisallowMultipleComponent]
public class BuildingDefense : MonoBehaviour
{
    [Header("건물 방어 데이터")]
    [Min(0f)] public float defensePower = 0f;
    [Min(0f)] public float minimumDamage = 5f;

    // 根据当前防御力计算最终伤害，并保证一次攻击至少造成 minimumDamage 点伤害。
    // 현재 방어력으로 최종 피해를 계산하고, 한 번의 공격이 최소 minimumDamage만큼 피해를 주도록 보장한다.
    public float CalculateFinalDamage(float incomingDamage)
    {
        return Mathf.Max(minimumDamage, incomingDamage - defensePower);
    }

    // 兼容旧数据迁移时使用的初始化入口。
    // 기존 데이터를 이전할 때 사용하는 초기화 입구다.
    public void SetDefensePower(float newDefensePower)
    {
        defensePower = Mathf.Max(0f, newDefensePower);
    }
}
