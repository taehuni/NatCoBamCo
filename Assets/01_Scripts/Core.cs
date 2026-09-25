using UnityEngine;

public class Core : MonoBehaviour, IDamageable
{
    public float curHp = 100f; // 현재 체력 설정
    public float maxHp = 100f; // 최대 체력 설정
    public float defensePower = 0f;
    private bool defeatReported;

    void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestoreCoreHealth(this);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.RememberCoreHealth(this);
    }

    void Update()
    {
        GameOver();
    }

    // 统一受伤入口：通过 IDamageable 接收伤害，并保留原来的防御计算。
    // 통합 피해 입구: IDamageable로 피해를 받고 기존 방어 계산을 유지한다.
    public void TakeDamage(float damage)
    {
        if (defeatReported || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;
        // 방어력을 고려한 데미지 계산 (방어력이 데미지를 상쇄)
        float finalDamage = Mathf.Max(0, damage - defensePower);
        curHp -= finalDamage;
        Debug.Log($"Core가 {finalDamage}의 데미지를 입었습니다! 남은 체력: {curHp}");
        GameOver();
    }

    // 保留旧调用入口，实际受伤逻辑统一交给 TakeDamage。
    // 기존 호출 입구를 유지하고 실제 피해 처리는 TakeDamage에 맡긴다.
    public void GetDamage(float damage)
    {
        TakeDamage(damage);
    }

    public void Heal(float healAmount)
    {
        if (defeatReported) return;
        if (curHp == maxHp)
        {
            return;
        }
        else
        {
            curHp += healAmount;
            if (curHp > maxHp)
            {
                curHp = maxHp;
            }
        }

        //자원 소모 추가해야 함

    }

    void GameOver()
    {
        if (curHp <= 0 && !defeatReported && GameManager.Instance != null)
        {
            curHp = 0;
            defeatReported = true;
            GameManager.Instance.EndGame();
        }
    }
}
