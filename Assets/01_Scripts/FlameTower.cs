using UnityEngine;
using System.Collections;

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

            if (enemy != null)
            {
                enemy.TakeDamage(damagePerTick);
            }
        }
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
