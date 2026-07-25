using UnityEngine;

// 생존자 본체. 이제 데이터 클래스가 아니라 EnemyAI 처럼 3D 프리팹에 붙는 진짜 MonoBehaviour.
// 역할은 3가지로 고정: 정비공(Mechanic) / 채집가(Gatherer) / 연구원(Researcher)
//
// 정비공: 밤에 손상된 타워로 이동해서 수리 (SurvivorMechanicBehaviour 가 담당)
// 채집가: 거주구역에 대기, 낮에 플레이어가 자원 채집할 때 획득량 증가 (SurvivorManager.GetGatherBonus)
// 연구원: 연구실에 대기, 연구 시간 감소 (SurvivorManager.GetResearchSpeedMultiplier)
[RequireComponent(typeof(SurvivorMovement))]
public class SurvivorAI : MonoBehaviour
{
    public enum SurvivorRole
    {
        Mechanic,
        Gatherer,
        Researcher
    }

    [Header("기본 정보")]
    public string survivorName = "이름 없는 생존자";
    public SurvivorRole role;
    public int level = 1;

    [Header("역할 능력치 (미정 · 참고 수치, 레벨업 시 증가)")]
    public float repairPower = 5f;             // 정비공 - 초당 수리량
    public float gatherBonus = 0.15f;          // 채집가 - 채집량 배율 보너스
    public float researchSpeedBonus = 0.15f;   // 연구원 - 연구 시간 감소율

    [Header("상태")]
    public bool isInjured;
    public float recoveryTimeLeft;

    [Header("고정 위치 (채집가 -> 거주구역, 연구원 -> 연구실)")]
    [Tooltip("비어있으면 SurvivorManager.AddSurvivor() 가 역할에 맞는 건물을 찾아서 자동으로 채워줌")]
    public Transform homePoint;

    private SurvivorMovement movement;

    public bool IsAvailable
    {
        get { return !isInjured; }
    }

    void Awake()
    {
        movement = GetComponent<SurvivorMovement>();

        if (movement == null)
        {
            movement = gameObject.AddComponent<SurvivorMovement>();
        }
    }

    void Update()
    {
        TickRecovery(Time.deltaTime);
    }

    // 채집가/연구원 전용: 정해진 자리로 이동. 정비공은 SurvivorMechanicBehaviour 가 매번 다른 타워로 보내므로 호출 안 함.
    // SurvivorManager.AddSurvivor() 에서 homePoint 를 채운 직후 호출된다.
    public void GoHome()
    {
        if (role == SurvivorRole.Mechanic)
        {
            return;
        }

        if (homePoint == null)
        {
            Debug.Log($"{survivorName} 배치할 위치(homePoint)가 없습니다");
            return;
        }

        movement.MoveToPosition(homePoint.position);
    }

    public void LevelUp()
    {
        level++;

        repairPower += 1f;
        gatherBonus += 0.05f;
        researchSpeedBonus += 0.05f;

        Debug.Log($"{survivorName} 레벨업 -> Lv.{level}");
    }

    public void ApplyInjury(float recoveryTime)
    {
        isInjured = true;
        recoveryTimeLeft = recoveryTime;
    }

    void TickRecovery(float deltaTime)
    {
        if (!isInjured)
        {
            return;
        }

        recoveryTimeLeft -= deltaTime;

        if (recoveryTimeLeft <= 0f)
        {
            isInjured = false;
            recoveryTimeLeft = 0f;
            Debug.Log($"{survivorName} 부상 회복 완료");
        }
    }
}
