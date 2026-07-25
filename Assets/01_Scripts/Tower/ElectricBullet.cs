using UnityEngine;
using System.Collections.Generic;

public class ElectricBullet : MonoBehaviour
{
    private EnemyAI target; //敌人目标 / 적 타겟

    private int damage; //伤害 / 데미지
    private float coldPower; //减速效果 / 감속 효과
    private float coldTime; //减速持续时间 / 감속 지속 시간
    private float paralyzeStackIncrease; //麻痹叠加层数 / 마비 스택 증가량
    private float paralyzeDuration; //麻痹时间 / 마비 지속 시간
    private int maxTargets; //弹射数量 / 연쇄 공격 가능한 최대 대상 수
    private int hitCount; //当前弹射次数 / 현재 명중한 횟수
    public float chainRange = 5f; //继续弹射检测范围，没有目标就销毁炮弹 / 다음 연쇄 대상을 찾는 범위, 대상이 없으면 포탄을 제거
    private LayerMask enemyLayer; //敌人图层 / 적 레이어
    private List<EnemyAI> hitEnemies = new List<EnemyAI>(); //弹射攻击敌人记录列表 / 이미 명중한 적을 기록하는 리스트
    private bool hasHit; //确保碰撞和距离检测只触发一次HitTarget的判断条件 / 충돌 검사와 거리 검사가 HitTarget을 중복 호출하지 않도록 막는 조건

    private bool hasDefenseReduction;//是否减防
    private float defenseReductionAmount;//减防强度
    private float defenseReductionDuration;//减防时间
    public float moveSpeed = 20f; //炮弹速度 / 포탄 이동 속도
    public float hitDistance = 0.2f;


    void Update()
    {
        if (target == null) //没有敌人了就销毁炮弹 / 타겟이 없으면 포탄 제거
        {
            Destroy(gameObject);
            return;
        }

        FlyAtEnemy(); //飞向敌人 / 적을 향해 이동
        CheckHit(); //距离检测，当炮弹过快时使用 / 포탄이 너무 빠를 때 사용하는 거리 기반 명중 검사
    }

    // 从防御塔把数值赋给炮弹
    // 방어 타워의 공격 정보를 포탄에 전달하여 초기화
    public void Init(
    EnemyAI targetEnemy,
    int bulletDamage,
    float bulletColdPower,
    float bulletColdTime,
    float bulletParalyzeStackIncrease,
    float bulletParalyzeDuration,
    int maxTargets,
    LayerMask enemyLayer,
    bool hasDefenseReduction,
    float defenseReduceAmount,
    float defenseReduceTime)
    {
        target = targetEnemy;
        damage = bulletDamage;
        coldPower = bulletColdPower;
        coldTime = bulletColdTime;
        paralyzeStackIncrease = bulletParalyzeStackIncrease;
        paralyzeDuration = bulletParalyzeDuration;
        this.maxTargets = maxTargets;
        this.enemyLayer = enemyLayer;
        this.hasDefenseReduction = hasDefenseReduction;
        this.defenseReductionAmount = defenseReduceAmount;
        this.defenseReductionDuration = defenseReduceTime;
        Debug.Log("Bullet Init Target: " + targetEnemy.name);
    }

    void HitTarget()
    {
        if (hasHit)
        {
            return;
        }
        hasHit = true;


        target.TakeDamage(damage); //造成伤害 / 데미지 적용
        target.SetSpeedDown(coldPower, coldTime); //减速 / 감속 적용
        target.AddParalyzeStack(paralyzeStackIncrease, paralyzeDuration); //叠加麻痹层数 / 마비 스택 추가
        //如果有减防效果
        if (hasDefenseReduction)
        {
            target.ReduceDefensePower(defenseReductionAmount, defenseReductionDuration);
        }
        hitEnemies.Add(target); //将当前目标加到被弹射攻击的敌人的列表，用来记录被攻击过的敌人 / 현재 타겟을 이미 명중한 적 리스트에 추가
        hitCount++; //现在弹射数增加 / 현재 명중 횟수 증가

        if (hitCount >= maxTargets) //如果到达次数就停止弹射，销毁炮弹 / 최대 명중 수에 도달하면 연쇄를 멈추고 포탄 제거
        {
            Destroy(gameObject);
            return;
        }

        EnemyAI nextTarget = FindNextTarget(); //找到下一个目标，离被攻击目标最近的 / 현재 타겟 주변에서 가장 가까운 다음 타겟 찾기

        if (nextTarget == null) //没有下个目标就销毁炮弹 / 다음 타겟이 없으면 포탄 제거
        {
            Destroy(gameObject);
            return;
        }

        target = nextTarget; //把当前的目标变成找到的下一个目标 / 현재 타겟을 다음 타겟으로 변경
        hasHit = false; //重置命中判断，让炮弹可以命中下一个目标 / 다음 타겟을 명중할 수 있도록 명중 상태 초기화
    }

    EnemyAI FindNextTarget()
    {
        //在被攻击的敌人为坐标，找这个位置chainRange范围内的所有其他碰撞体
        //현재 명중한 적의 위치를 기준으로 chainRange 안에 있는 다른 충돌체를 찾음
        Collider[] colliders = Physics.OverlapSphere(target.transform.position, chainRange, enemyLayer);

        //初始化
        //가장 가까운 적을 찾기 위한 초기값 설정
        EnemyAI closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyAI enemy = colliders[i].GetComponentInParent<EnemyAI>(); //遍历找到的其他碰撞体，找他们父类的EnemyAI / 찾은 충돌체의 부모 오브젝트에서 EnemyAI를 찾음

            if (enemy == null)
            {
                //没找到组件就跳到下一个
                //EnemyAI를 찾지 못하면 다음 후보로 넘어감
                continue;
            }

            if (hitEnemies.Contains(enemy))
            {
                //如果已经被攻击过了就跳过
                //이미 명중한 적이면 다시 공격하지 않고 건너뜀
                continue;
            }

            //计算距离
            //현재 타겟과 후보 적 사이의 거리 계산
            float distance = Vector3.Distance(target.transform.position, enemy.transform.position);

            //找到最小的距离
            //가장 가까운 적을 찾기 위해 최소 거리 갱신
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        //返回那个最小距离的目标敌人
        //가장 가까운 적을 다음 타겟으로 반환
        return closestEnemy;
    }

    //飞向敌人
    //적을 향해 이동
    void FlyAtEnemy()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            moveSpeed * Time.deltaTime
        );
    }

    //碰撞检测
    //충돌 기반 명중 검사
    void OnTriggerEnter(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();

        if (enemy == null)
        {
            return;
        }

        if (enemy == target)
        {
            HitTarget();
        }

    }

    //距离检测
    //거리 기반 명중 검사
    void CheckHit()
    {
        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance <= hitDistance)
        {
            HitTarget();
        }
    }
}
