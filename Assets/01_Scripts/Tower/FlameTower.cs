using UnityEngine;
using System.Collections;
using static UnityEngine.GraphicsBuffer;

public class FlameTower : MonoBehaviour
{
    [Header("감지")]
    public float detectRange = 10f;
    public LayerMask enemyLayer;

    [Header("데미지")]
    //화염 넓이 조절
    public float flameLength = 6f;
    public float flameWidth = 2f; 

    public int damagePerTick = 1; //틱당 데미지
    public float damageInterval = 0.2f; //데미지 틱

    [Header("딜레이")]
    //화염 지속 시간
    public float fireDuration = 2f;
    //딜레이
    public float cooldown = 3f;

    private bool isAttacking = false;

    [Header("능력")]
    public float executionPer = 0.1f; //현재 10% 이하 처형. 기본값을 0으로 두고 이후 수치를 올리는 것으로 조정 가능.

    void Update()
    {
        if (!isAttacking)
        {
            EnemyAI target = FindTarget();

            if (target != null)
            {
                StartCoroutine(FlameAttack());
            }
        }
    }

    EnemyAI FindTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(
            transform.position,
            detectRange,
            enemyLayer);


        EnemyAI target = null;
        float closest = Mathf.Infinity;

        foreach (Collider col in enemies)
        {
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();

            if (enemy == null)
                continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < closest)
            {
                closest = distance;
                target = enemy;
            }
        }

        return target;
    }

    IEnumerator FlameAttack()
    {
        isAttacking = true;

        float timer = 0f;

        // 나중에 화염 이펙트 ON

        while (timer < fireDuration)
        {
            EnemyAI target = FindTarget();

            if (target == null)
                break;

            RotateToTarget(target);

            DealDamage();
            yield return new WaitForSeconds(damageInterval);

            timer += damageInterval;
        }

        // 나중에 화염 이펙트 OFF

        yield return new WaitForSeconds(cooldown);

        isAttacking = false;
    }

    void RotateToTarget(EnemyAI target)
    {
        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0;

        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                10f * Time.deltaTime);
        }
    }

    void DealDamage()
    {
        Vector3 center =
            transform.position +
            transform.forward * flameLength * 0.5f;

        Collider[] enemies = Physics.OverlapBox(
            center,
            new Vector3(flameWidth * 0.5f, 1f, flameLength * 0.5f),
            transform.rotation,
            enemyLayer);

        foreach (Collider col in enemies)
        {
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null && enemy.Health != null && enemy.Health.maxHp > 0f)
            {
                float hpPercent = enemy.Health.health / enemy.Health.maxHp;

                if (hpPercent <= executionPer)
                {
                    Debug.Log("처형");
                    enemy.Dead();
                }
                else
                {
                    enemy.TakeDamage(damagePerTick);
                }
            }
        }
    }

    //후에 추가될 업그레이드 기능
    void UpgradeDamage(int damage)
    {
        this.damagePerTick += damage;
    }

    void UpgradeDelay(float delay)
    {
        cooldown -= delay; //후에 %형식으로 조정 예정
    }

    void UpgradeRange(float length, float width)
    {
        flameLength += length;
        flameWidth += width;
    }

    void UpgradeExcute(float per)
    {
        executionPer += per;
    }

    // 디버그용 기즈모
    private void OnDrawGizmos()
    {
        // 탐지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 화염 범위
        Gizmos.color = Color.red;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.DrawWireCube(
            new Vector3(0, 1f, flameLength * 0.5f),
            new Vector3(flameWidth, 2f, flameLength));

        Gizmos.matrix = oldMatrix;
    }
}
