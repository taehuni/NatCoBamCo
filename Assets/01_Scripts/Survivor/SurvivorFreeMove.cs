using UnityEngine;

// 정비공 외 생존자(채집가/연구원)가 거주구역 근처를 자유롭게 배회하게 하는 컴포넌트.
// 정비공도 낮에는 같이 배회하다가 밤이 되면 SurvivorMechanicBehaviour 가 이동을 넘겨받는다.
[RequireComponent(typeof(SurvivorAI))]
[RequireComponent(typeof(SurvivorMovement))]
public class SurvivorFreeMove : MonoBehaviour
{
    [Header("배회 범위")]
    public float roamRadius = 8f;
    public float waitTimeAtPoint = 2f;

    [Header("밤 여부 (정비공 전용)")]
    [Tooltip("실제 게임에서는 GameManager의 밤 상태를 사용한다. GameManager가 없는 테스트 씬에서는 SetNight로 설정한다.")]
    public bool isNight;

    private SurvivorAI survivorAI;
    private SurvivorMovement movement;
    private float waitTimer;
    private bool waiting;
    private SurvivorMechanicBehaviour mechanic;
    private bool resumeRoaming = true;

    void Awake()
    {
        survivorAI = GetComponent<SurvivorAI>();
        movement = GetComponent<SurvivorMovement>();
        mechanic = GetComponent<SurvivorMechanicBehaviour>();
    }

    void Update()
    {
        var game = GameManager.Instance;
        if (game != null)
        {
            SetNight(game.currentPhase == GameManager.GamePhase.NightStart ||
                game.currentPhase == GameManager.GamePhase.Defense);
            if (game.IsGameOver || game.IsRestarting) return;
        }
        if (Time.timeScale <= 0f) return;

        if (survivorAI == null || !survivorAI.IsAvailable)
        {
            resumeRoaming = true;
            return;
        }

        if (survivorAI.state != SurvivorAI.SurvivorState.Rescued)
        {
            return; // 구출되기 전에는 배회하지 않음
        }

        if (survivorAI.role == SurvivorAI.SurvivorRole.Mechanic && isNight)
        {
            return; // 정비공 밤: SurvivorMechanicBehaviour 가 이동 전담
        }

        if (survivorAI.homePoint == null)
        {
            return;
        }

        if (!movement.IsReady) return;
        if (survivorAI.role == SurvivorAI.SurvivorRole.Mechanic && resumeRoaming)
        {
            resumeRoaming = false;
            waiting = false;
            PickNextPoint();
            return;
        }

        if (waiting)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                waiting = false;
                PickNextPoint();
            }

            return;
        }

        if (movement.HasArrived())
        {
            waiting = true;
            waitTimer = waitTimeAtPoint;
        }
    }

    void PickNextPoint()
    {
        Vector2 offset = Random.insideUnitCircle * roamRadius;
        Vector3 target = survivorAI.homePoint.position + new Vector3(offset.x, 0f, offset.y);

        movement.MoveToPosition(target);
    }

    // 낮/밤 시스템에서 호출 (SurvivorMechanicBehaviour.SetNight() 와 동일한 인터페이스)
    public void SetNight(bool night)
    {
        if (isNight != night) resumeRoaming = true;
        isNight = night;
        // 수리 이동을 먼저 정리하므로 두 Update의 실행 순서와 무관하게 배회를 재개한다.
        if (mechanic != null) mechanic.SetNight(night);
    }

    void OnDisable()
    {
        waiting = false;
        waitTimer = 0f;
        resumeRoaming = true;
    }

    void OnDrawGizmosSelected()
    {
        if (survivorAI == null)
        {
            survivorAI = GetComponent<SurvivorAI>();
        }

        Transform anchor = survivorAI != null && survivorAI.homePoint != null ? survivorAI.homePoint : transform;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(anchor.position, roamRadius);
    }
}
