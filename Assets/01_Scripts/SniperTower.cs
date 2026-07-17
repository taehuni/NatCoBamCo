using UnityEngine;
using System.Collections.Generic;

public class SniperTower : MonoBehaviour
{
    public float damage; // 伤害 / 데미지
    public float attackRange; // 攻击范围 / 공격 범위
    public float criticalChance = 0.2f; // 暴击率，0.2 = 20% / 치명타 확률, 0.2 = 20%
    public float criticalMultiplier = 2f; // 暴击伤害倍率，2 = 造成 2 倍伤害 / 치명타 데미지 배율, 2 = 2배 데미지
    public float attackInterval = 2f; // 攻击间隔 / 공격 간격
    private float nextAttackTime; // 下一次可以攻击的时间 / 다음 공격 가능 시간
    public float executeChance = 0.05f; // 处决几率，0.05 = 5% / 처형 확률, 0.05 = 5%
    public float ignoreDefenseRate = 0.2f; // 无视防御率，0.2 = 无视 20% 减伤 / 방어 무시 비율, 0.2 = 20% 피해 감소 무시
    public int level = 1; // 当前等级 / 현재 레벨
    public int maxLevel = 4; // 最大等级 / 최대 레벨
    public float eliteDamageBonusRate = 0.2f; // 对 Elite 增伤比例，0.2 = 增伤 20% / Elite 대상 추가 데미지 비율, 0.2 = 20% 추가 데미지
    public float bossDamageBonusRate = 0.3f; // 对 Boss 增伤比例，0.3 = 增伤 30% / Boss 대상 추가 데미지 비율, 0.3 = 30% 추가 데미지
    public LayerMask enemyLayer; // 敌人图层 / 적 레이어
    public GameObject bulletPrefab; // 子弹预制体 / 탄환 프리팹
    public Transform firePoint; // 发射位置 / 발사 위치
    public float rotateSpeed = 180f;
    private List<EnemyAI> enemiesInRange = new List<EnemyAI>(); // 在攻击范围内的敌人 / 공격 범위 안에 있는 적 목록
    private EnemyAI targetEnemy;
    void Update()
    {
        // 更新当前攻击范围内的敌人列表
        // 현재 공격 범위 안의 적 목록 갱신
        UpdateEnemiesInRange();

        // 获取最先进入攻击范围的敌人作为目标
        // 가장 먼저 공격 범위에 들어온 적을 타겟으로 사용
        targetEnemy = GetFirstEnteredEnemy();

        if (targetEnemy != null)
        {
            LookAtEnemy(targetEnemy);
        }
        // 如果当前时间到达下一次攻击时间，就尝试攻击
        // 현재 시간이 다음 공격 가능 시간에 도달하면 공격을 시도
        if (Time.time >= nextAttackTime)
        {
            bool attacked = Attack();

            // 只有真正发射成功后，才进入攻击冷却
            // 실제로 공격에 성공했을 때만 공격 쿨타임 적용
            if (attacked)
            {
                nextAttackTime = Time.time + attackInterval;
            }
        }
    }

    bool Attack()
    {
        // 如果子弹预制体或发射点没有设置，就不能攻击
        // 탄환 프리팹 또는 발사 위치가 없으면 공격 불가
        if (bulletPrefab == null || firePoint == null)
        {
            return false;
        }

        // 没有目标就不攻击
        // 타겟이 없으면 공격하지 않음
        if (targetEnemy == null)
        {
            return false;
        }

        // 在发射点生成子弹
        // 발사 위치에서 탄환 생성
        GameObject bulletObject = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // 获取子弹上的 SniperBullet 脚本
        // 생성된 탄환에서 SniperBullet 컴포넌트 가져오기
        SniperBullet bullet = bulletObject.GetComponent<SniperBullet>();

        if (bullet != null)
        {
            // 把狙击塔的攻击数据传给子弹
            // 저격 타워의 공격 데이터를 탄환에 전달
            bullet.Init(
                damage,
                criticalChance,
                criticalMultiplier,
                executeChance,
                ignoreDefenseRate,
                eliteDamageBonusRate,
                bossDamageBonusRate,
                targetEnemy
            );

            return true;
        }
        
        Destroy(bulletObject);
        return false;
    }

    void UpdateEnemiesInRange()
    {
        // 查找攻击范围内属于 enemyLayer 的碰撞体
        // 공격 범위 안에서 enemyLayer에 해당하는 충돌체 검색
        Collider[] colliders = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        // 从后往前遍历，删除已经死亡或离开范围的敌人
        // 뒤에서부터 순회하며 죽었거나 범위를 벗어난 적 제거
        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (enemiesInRange[i] == null)
            {
                enemiesInRange.RemoveAt(i);
            }
            else
            {
                float distance = Vector3.Distance(transform.position, enemiesInRange[i].transform.position);

                if (distance > attackRange)
                {
                    enemiesInRange.RemoveAt(i);
                }
            }
        }

        // 把新进入范围的敌人加入列表
        // 새로 범위에 들어온 적을 리스트에 추가
        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyAI enemy = colliders[i].GetComponentInParent<EnemyAI>();

            if (enemy == null)
            {
                continue;
            }

            if (!enemiesInRange.Contains(enemy))
            {
                enemiesInRange.Add(enemy);
            }
        }
    }

    EnemyAI GetFirstEnteredEnemy()
    {
        // 如果列表里没有敌人，就返回空
        // 리스트에 적이 없으면 null 반환
        if (enemiesInRange.Count == 0)
        {
            return null;
        }

        // 返回最先进入攻击范围的敌人
        // 가장 먼저 공격 범위에 들어온 적 반환
        return enemiesInRange[0];
    }

    public void LevelUp()
    {
        // 如果已经达到最大等级，就不能继续升级
        // 이미 최대 레벨이면 더 이상 업그레이드하지 않음
        if (level >= maxLevel)
        {
            return;
        }

        level++;
        damage += 10;
        attackRange += 5;
        attackInterval -= 0.1f;
        executeChance += 0.025f;
        ignoreDefenseRate += 0.05f;
        eliteDamageBonusRate += 0.05f;
        bossDamageBonusRate += 0.05f;
        criticalChance += 0.05f;
        criticalMultiplier += 0.25f;
    }

    void LookAtEnemy(EnemyAI targetEnemy)
    {
        if (targetEnemy == null)
        {
            return;
        }

        Vector3 direction = targetEnemy.transform.position - transform.position;

        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
             targetRotation, rotateSpeed * Time.deltaTime);
        }

    }

    void OnDrawGizmosSelected()
    {
        // 在 Scene 视图中画出攻击范围，方便调试
        // Scene 뷰에서 공격 범위를 표시하여 디버깅에 사용
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
