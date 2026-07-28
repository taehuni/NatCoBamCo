using System.Collections.Generic;
using UnityEngine;

// 敌人死亡模块：负责死亡保护、坦克自爆、最后销毁敌人对象。
// 적 사망 모듈: 중복 사망 방지, 탱크 자폭, 최종 오브젝트 제거를 담당한다.
// EnemyHealth 发现血量归零后，会通过 EnemyAI.Dead() 转到这里处理。
// EnemyHealth가 체력 0을 감지하면 EnemyAI.Dead()를 통해 여기로 넘어와 처리한다.
// 死亡处理模块：负责死亡保护、坦克自爆、销毁敌人。
public class EnemyDeathHandler : MonoBehaviour
{
    [Header("Tank Explosion Data / 탱크 자폭 데이터")]
    public float explosionRange;
    public float explosionDamage;
    public LayerMask explosionTargetLayer;

    private EnemyAI enemyAI;
    private bool isDead;

    public void Initialize(EnemyAI enemyAI)
    {
        // 缓存 EnemyAI，用来读取敌人类型，例如判断是不是 Tank。
        // EnemyAI를 캐시해서 적 종류를 읽는다. 예: Tank인지 판단한다.
        this.enemyAI = enemyAI;
    }

    public void Die()
    {
        // enemyAI 为空或者已经死过一次，就直接返回。
        // enemyAI가 없거나 이미 한 번 죽었으면 바로 반환한다.
        // isDead 可以避免同一帧多次受伤时重复执行自爆。
        // isDead는 같은 프레임에 여러 번 피해를 받아도 자폭이 중복 실행되지 않게 막는다.
        if (enemyAI == null || isDead)
        {
            return;
        }

        isDead = true;

        // 坦克型敌人死亡时触发范围自爆。
        // Tank 타입 적은 사망할 때 범위 자폭을 실행한다.
        if (enemyAI.ClassTraits != null && enemyAI.ClassTraits.enemyClass == EnemyAI.EnemyClass.Tank)
        {
            Explode();
        }

        // 死亡效果处理完后销毁敌人。
        // 사망 효과 처리가 끝나면 적 오브젝트를 제거한다.
        Destroy(gameObject);
    }

    void Explode()
    {
        // 找到自爆范围内指定图层的碰撞体。
        // 자폭 범위 안에서 지정한 레이어의 콜라이더를 찾는다.
        Collider[] targets = Physics.OverlapSphere(
            transform.position,
            explosionRange,
            explosionTargetLayer
        );

        // 用 List 记录已经受到过自爆伤害的建筑。
        // 이미 자폭 피해를 받은 건물을 List에 기록한다.
        // 这样一个建筑有多个 Collider 时，也只会被炸一次。
        // 이렇게 하면 한 건물에 Collider가 여러 개 있어도 한 번만 피해를 받는다.
        List<DamageableBuilding> damagedBuildings = new List<DamageableBuilding>();

        for (int i = 0; i < targets.Length; i++)
        {
            // 碰撞体可能在建筑子物体上，所以向父级查找 DamageableBuilding。
            // 콜라이더가 건물의 자식 오브젝트에 있을 수 있으므로 부모 쪽에서 DamageableBuilding을 찾는다.
            DamageableBuilding building = targets[i].GetComponentInParent<DamageableBuilding>();

            if (building == null)
            {
                continue;
            }

            // 如果这个建筑已经被炸过，跳过，避免重复伤害。
            // 이미 피해를 준 건물이면 건너뛰어서 중복 피해를 막는다.
            if (damagedBuildings.Contains(building))
            {
                continue;
            }

            // 对建筑造成自爆伤害，并记录它已经被处理过。
            // 건물에 자폭 피해를 주고, 처리된 건물로 기록한다.
            building.GetDamage(explosionDamage);
            damagedBuildings.Add(building);
        }
    }
}
