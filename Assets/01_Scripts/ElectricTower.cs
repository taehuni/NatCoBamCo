using UnityEngine;
using System.Collections.Generic;

public class ElectricTower : MonoBehaviour
{
    public Transform firePoint; //开火位置 / 발사 위치
    public GameObject bulletPrefab; //炮弹预制体 / 포탄 프리팹
    public float attackRange = 10f; //攻击范围 / 공격 범위
    public float attackInterval = 0.9f; //攻击间隔 / 공격 간격
    public int damage = 2; //伤害 / 데미지
    public LayerMask enemyLayer; //敌人图层 / 적 레이어
    public float coldTime = 2f; //减速持续时间 / 감속 지속 시간
    public float coldPower = 0.3f; //减速效果，0.3表示减速30% / 감속 효과, 0.3은 30% 감속
    public float maxColdPower = 0.6f; //最大减速效果，0.6表示减速60% / 최대 감속 효과, 0.6은 60% 감속
    public int maxTargets = 3; //攻击弹射数 / 연쇄 공격 가능한 대상 수
    public int level = 1; //防御塔等级 / 방어 타워 레벨
    public int maxlevel = 4; //最大等级 / 최대 레벨
    public float paralyzeDuration = 2f; //麻痹持续时间 / 마비 지속 시간
    public float paralyzeStackIncrease = 10; //麻痹叠加层数 / 마비 스택 증가량
    public bool hasDefenseReduction; //是否减防
    public float defenseReduceAmount = 0.2f; //减防强度
    public float defenseReduceTime = 3f; //减防时间
    private float nextAttackTime; //下次攻击时间 / 다음 공격 가능 시간
    private List<EnemyAI> enemiesInRange = new List<EnemyAI>(); //在攻击范围内的敌人 / 공격 범위 안에 있는 적 목록


    //每帧检查是否到达攻击时间
    //매 프레임 공격 시간이 되었는지 확인
    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            bool attacked = Attack();

            if (attacked)
            {
                nextAttackTime = Time.time + attackInterval;
            }
        }
    }

    //更新攻击范围内的敌人列表
    //공격 범위 안의 적 리스트를 갱신
    void UpdateEnemiesInRange()
    {
        //在攻击范围找有敌人layer的碰撞体
        //공격 범위 안에서 enemyLayer에 해당하는 충돌체를 찾음
        Collider[] colliders = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        //从后遍历，如果这个敌人是无效的敌人就从列表中删除
        //뒤에서부터 순회하며 죽었거나 유효하지 않은 적을 리스트에서 제거
        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (enemiesInRange[i] == null)
            {
                enemiesInRange.RemoveAt(i);
            }
            else
            {
                float distance = Vector3.Distance(transform.position, enemiesInRange[i].transform.position);

                //如果敌人移动到攻击范围之外就从列表中删除
                //적이 공격 범위 밖으로 이동하면 리스트에서 제거
                if (distance > attackRange)
                {
                    enemiesInRange.RemoveAt(i);
                }
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            //找到碰撞体父对象中的EnemyAI
            //충돌체의 부모 오브젝트에서 EnemyAI를 찾음
            EnemyAI enemy = colliders[i].GetComponentInParent<EnemyAI>();

            //没找到就继续下一个
            //EnemyAI를 찾지 못하면 다음 충돌체로 넘어감
            if (enemy == null)
            {
                continue;
            }

            //如果敌人列表里没有这个敌人就添加
            //리스트에 없는 적이면 새로 추가
            if (!enemiesInRange.Contains(enemy))
            {
                enemiesInRange.Add(enemy);
            }
        }
    }

    //返回搜索到的敌人列表中的第一个敌人(最先进入攻击范围的敌人)
    //검색된 적 리스트의 첫 번째 적, 즉 가장 먼저 공격 범위에 들어온 적을 반환
    EnemyAI GetFirstEnteredEnemy()
    {
        if (enemiesInRange.Count == 0)
        {
            return null;
        }

        return enemiesInRange[0];
    }

    bool Attack()
    {
        if (bulletPrefab == null || firePoint == null)
        {
            return false;
        }

        //更新搜索到的敌人列表
        //현재 공격 범위 안의 적 리스트 갱신
        UpdateEnemiesInRange();

        EnemyAI targetEnemy = GetFirstEnteredEnemy(); //把最先进入攻击范围的敌人拿过来当做目标 / 가장 먼저 공격 범위에 들어온 적을 타겟으로 사용

        if (targetEnemy == null) //如果没有目标就不攻击 / 타겟이 없으면 공격하지 않음
        {
            return false;
        }

        //在开火位置生成炮弹
        //발사 위치에서 포탄 생성
        GameObject bulletObject = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        //拿到炮弹上的ElectricBullet组件
        //생성된 포탄에서 ElectricBullet 컴포넌트를 가져옴
        ElectricBullet bullet = bulletObject.GetComponent<ElectricBullet>();
        
        //如果炮弹不为空
        //포탄 컴포넌트가 있으면
        if (bullet != null)
        {
            //初始化炮弹
            //포탄에 타겟과 공격 데이터를 전달하여 초기화
            bullet.Init(
                targetEnemy,
                damage,
                coldPower,
                coldTime,
                paralyzeStackIncrease,
                paralyzeDuration,
                maxTargets,
                enemyLayer,
                hasDefenseReduction,
                defenseReduceAmount,
                defenseReduceTime
            );
            return true;
        }
        
        Destroy(bulletObject);
        return false;

    }

    //升级
    //타워 업그레이드
    public void LevelUp()
    {
        if (level >= maxlevel)
        {
            return;
        }
        level++;
        if (level >= maxlevel)
        {
            hasDefenseReduction = true;
        }
        damage += 2;
        attackRange += 2f;
        attackInterval -= 0.1f;
        coldTime += 0.5f;
        coldPower += 0.05f;
        coldPower = Mathf.Clamp(coldPower, 0f, maxColdPower);
        if (level % 2 == 0) //每2级增加一个弹射目标 / 2레벨마다 연쇄 대상 수 1 증가
        {
            maxTargets++;
        }
        paralyzeDuration += 0.2f;
        paralyzeStackIncrease += 5;
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
