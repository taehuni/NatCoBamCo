using UnityEngine;

public class Tower : MonoBehaviour
{
    public float attackRange = 10f;
    public float attackInterval = 1f;
    public int damage = 10;
    public LayerMask enemyLayer;
    public int level = 1;
    public int maxLevel = 4;

    private float nextAttackTime;
    private EnemyAI currentTarget;


    void Start()
    {

    }

    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            Attack();

            nextAttackTime = Time.time + attackInterval;
        }
    }

    void Attack()
    {

        //원형 범위에 enemy layer 있는 놈이 찾아
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        //없으면 동작이 안해
        if (enemies.Length == 0)
        {
            return;
        }
        //첫번째 찾는 놈
        Collider target = enemies[0];

        EnemyAI enemy = target.GetComponentInParent<EnemyAI>();
        currentTarget = enemy;
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
        }

    }

    // 연구 완료 시 기존 타워와 이후 건설하는 타워에 동일한 단계를 적용한다.
    public void LevelUp()
    {
        if (level >= maxLevel) return;
        level++;
        UpgradeDamage(5);
        UpgradeDelay(0.1f);
    }

    void UpgradeDamage(int damage)
    {
        this.damage += damage;
    }

    void UpgradeDelay(float delay)
    {
        attackInterval = Mathf.Max(0.1f, attackInterval - delay);
    }

    //기즈모 그리기
    private void OnDrawGizmos()
    {
        // 공격 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 현재 타겟
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}
