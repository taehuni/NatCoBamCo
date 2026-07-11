using UnityEngine;

public class SniperBullet : MonoBehaviour
{
    private float damage; // 伤害 / 데미지
    private float criticalChance; // 暴击率，0.2 = 20% / 치명타 확률, 0.2 = 20%
    private float criticalMultiplier; // 暴击伤害倍率，2 = 造成 2 倍伤害 / 치명타 데미지 배율, 2 = 2배 데미지
    private float executeChance; // 处决几率，0.05 = 5% / 처형 확률, 0.05 = 5%
    private float ignoreDefenseRate; // 无视防御率，0.2 = 无视 20% 减伤 / 방어 무시 비율, 0.2 = 20% 피해 감소 무시
    private float eliteDamageBonusRate; // 对 Elite 增伤比例，0.2 = 增伤 20% / Elite 대상 추가 데미지 비율, 0.2 = 20% 추가 데미지
    private float bossDamageBonusRate; // 对 Boss 增伤比例，0.3 = 增伤 30% / Boss 대상 추가 데미지 비율, 0.3 = 30% 추가 데미지
    private EnemyAI targetEnemy; // 当前目标敌人 / 현재 타겟 적
    private bool hasHit; // 防止同一颗子弹重复命中 / 같은 탄환이 중복 명중하는 것을 방지
    public float hitDistance = 0.2f; // 命中距离 / 명중 거리
    public float moveSpeed; // 子弹移动速度 / 탄환 이동 속도

    void Update()
    {
        // 如果目标已经不存在，就销毁子弹
        // 타겟이 사라졌으면 탄환 제거
        if (targetEnemy == null)
        {
            Destroy(gameObject);
            return;
        }

        FlyAtEnemy();
        CheckHit();
    }

    void HitTarget()
    {
        // 如果已经命中过，就直接返回，避免重复造成伤害
        // 이미 명중했다면 중복 데미지를 막기 위해 바로 반환
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        float finalDamage = damage; // 先从基础伤害开始计算 / 기본 데미지부터 계산 시작

        // 如果目标是精英怪，增加伤害
        // 타겟이 Elite라면 추가 데미지 적용
        if (targetEnemy.enemyType == EnemyAI.EnemyType.Elite)
        {
            finalDamage *= (1 + eliteDamageBonusRate);
        }

        // 如果目标是 Boss，增加伤害
        // 타겟이 Boss라면 추가 데미지 적용
        if (targetEnemy.enemyType == EnemyAI.EnemyType.Boss)
        {
            finalDamage *= (1 + bossDamageBonusRate);
        }

        // 如果目标是普通怪，有概率触发处决
        // 타겟이 일반 몬스터라면 확률적으로 처형 발동
        if (targetEnemy.enemyType == EnemyAI.EnemyType.Normal)
        {
            if (Random.value <= executeChance)
            {
                // 处决成功后直接让敌人死亡，之后可以在这里添加处决特效
                // 처형 성공 시 적을 즉시 사망 처리, 이후 처형 이펙트를 추가할 수 있음
                targetEnemy.Dead();
            }
        }

        // 根据暴击率判断是否暴击
        // 치명타 확률에 따라 치명타 여부 판단
        if (Random.value <= criticalChance)
        {
            finalDamage *= criticalMultiplier;
        }

        // 把计算后的伤害交给 EnemyAI，再由 EnemyAI 处理防御减伤
        // 계산된 데미지를 EnemyAI에 전달하고, EnemyAI에서 방어 감소 계산 처리
        targetEnemy.TakeDamage(finalDamage, ignoreDefenseRate);
    }

    public void FlyAtEnemy()
    {
        // 子弹朝目标敌人移动
        // 탄환이 타겟 적을 향해 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetEnemy.transform.position,
            moveSpeed * Time.deltaTime
        );
    }

    public void Init(
        float towerDamage,
        float towerCriticalChance,
        float towerCriticalMultiplier,
        float towerExecuteChance,
        float towerIgnoreDefenseRate,
        float towerEliteDamageBonusRate,
        float towerBossDamageBonusRate,
        EnemyAI towerTargetEnemy)
    {
        // 从狙击塔接收本次攻击需要的数据
        // 저격 타워로부터 이번 공격에 필요한 데이터를 전달받음
        damage = towerDamage;
        criticalChance = towerCriticalChance;
        criticalMultiplier = towerCriticalMultiplier;
        executeChance = towerExecuteChance;
        ignoreDefenseRate = towerIgnoreDefenseRate;
        eliteDamageBonusRate = towerEliteDamageBonusRate;
        bossDamageBonusRate = towerBossDamageBonusRate;
        targetEnemy = towerTargetEnemy;
    }

    void CheckHit()
    {
        // 使用距离检测判断是否命中目标
        // 거리 검사를 사용하여 타겟 명중 여부 판단
        float distance = Vector3.Distance(transform.position, targetEnemy.transform.position);

        if (distance <= hitDistance)
        {
            HitTarget();
            Destroy(gameObject);
        }
    }

    private void OggerEnter(Collider other)
    {
        // 当前函数名不是 Unity 的触发函数名，如果要使用触发器，需要改成 OnTriggerEnter
        // 현재 함수명은 Unity의 트리거 함수명이 아님, 트리거를 사용하려면 OnTriggerEnter로 변경 필요
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();

        if (enemy == null)
        {
            return;
        }

        if (enemy == targetEnemy)
        {
            HitTarget();
            Destroy(gameObject);
        }
    }
}
