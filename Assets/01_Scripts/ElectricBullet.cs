using UnityEngine;

public class ElectricBullet : MonoBehaviour
{
    private EnemyAI target;

    private int damage;
    private float coldPower;
    private float coldTime;
    private int paralyzeStackIncrease;
    private float paralyzeDuration;

    public float moveSpeed = 20f;


    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        FlyAtEnemy();
    }

    //포탄 변수 초기화 从防御塔把数值赋给炮弹
    public void Init(
    EnemyAI targetEnemy,
    int bulletDamage,
    float bulletColdPower,
    float bulletColdTime,
    int bulletParalyzeStackIncrease,
    float bulletParalyzeDuration)
    {
        target = targetEnemy;
        damage = bulletDamage;
        coldPower = bulletColdPower;
        coldTime = bulletColdTime;
        paralyzeStackIncrease = bulletParalyzeStackIncrease;
        paralyzeDuration = bulletParalyzeDuration;
        Debug.Log("Bullet Init Target: " + targetEnemy.name);
    }

    void HitTarget()
    {

        target.TakeDamage(damage);
        target.SetSpeedDown(coldPower, coldTime);
        target.AddParalyzeStack(paralyzeStackIncrease, paralyzeDuration);

        Destroy(gameObject);
    }

    void FlyAtEnemy()
    {

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            moveSpeed * Time.deltaTime
        );
    }

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
}
