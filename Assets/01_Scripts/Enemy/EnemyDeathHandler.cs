using System.Collections.Generic;
using UnityEngine;

// 死亡处理模块：负责敌人死亡时的特殊逻辑，例如坦克自爆，然后销毁敌人。
// 사망 처리 모듈: 적이 죽을 때의 특수 로직을 담당. 예: 탱크 자폭 후 적 제거.
public class EnemyDeathHandler : MonoBehaviour
{
    private EnemyAI enemyAI;
    // 防止同一个敌人在同一帧或短时间内重复执行死亡逻辑。
    // 같은 적이 같은 프레임이나 짧은 시간 안에 사망 로직을 여러 번 실행하는 것을 방지.
    private bool isDead;

    // 保存 EnemyAI 引用，用于读取敌人类型、自爆范围、自爆伤害等数据。
    // EnemyAI 참조를 저장해서 적 종류, 자폭 범위, 자폭 데미지 등을 읽음.
    public void Initialize(EnemyAI enemyAI)
    {
        this.enemyAI = enemyAI;
    }

    public void Die()
    {
        // enemyAI 不存在或已经死过时直接返回，避免重复自爆、重复销毁。
        // enemyAI가 없거나 이미 죽은 상태면 바로 반환해서 중복 자폭, 중복 제거를 방지.
        if (enemyAI == null || isDead)
        {
            return;
        }

        isDead = true;

        // 坦克型敌人死亡时执行自爆，其他类型直接死亡。
        // 탱크형 적은 사망 시 자폭을 실행하고, 다른 타입은 바로 사망 처리.
        if (enemyAI.enemyClass == EnemyAI.EnemyClass.Tank)
        {
            Explode();
        }

        Destroy(gameObject);
    }

    // 坦克型敌人死亡自爆
    // 탱크형 적 사망 시 자폭
    void Explode()
    {
        // 在自爆范围内查找可受影响目标。
        // 자폭 범위 안에서 영향을 받을 대상을 찾음.
        Collider[] targets = Physics.OverlapSphere(
            transform.position,
            enemyAI.explosionRange,
            enemyAI.explosionTargetLayer
        );

        // 一个建筑可能有多个 Collider，用 List 记录已经受伤的建筑，避免重复扣血。
        // 하나의 건물에 Collider가 여러 개 있을 수 있으므로 이미 데미지를 받은 건물을 기록해서 중복 데미지를 방지.
        List<DamageableBuilding> damagedBuildings = new List<DamageableBuilding>();

        for (int i = 0; i < targets.Length; i++)
        {
            // 射线/范围可能检测到建筑子物体，所以向父级查找 DamageableBuilding。
            // 범위 감지가 건물의 자식 오브젝트를 잡을 수 있으므로 부모에서 DamageableBuilding을 찾음.
            DamageableBuilding building = targets[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            if (damagedBuildings.Contains(building))
            {
                continue;
            }

            building.GetDamage(enemyAI.explosionDamage);
            damagedBuildings.Add(building);
        }
    }
}
