using UnityEngine;

// 只管理两个巡逻点之间的往返和到点停留，由追逐脚本决定何时执行。
// 두 순찰 지점 사이의 왕복과 도착 후 대기만 관리하며, 실행 시점은 추적 스크립트가 결정한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovement))]
public class EnemyPatrolBehaviour : MonoBehaviour
{
    [Header("Patrol Route / 순찰 경로")]
    [Tooltip("첫 번째 순찰 지점입니다. 씬의 빈 오브젝트를 지정하고 적의 자식으로 두지 마세요. 순찰을 처음 시작하면 이 지점으로 이동합니다.")]
    public Transform startPoint;
    [Tooltip("두 번째 순찰 지점입니다. 시작 지점과 이 지점을 왕복합니다. 적의 자식으로 두지 마세요.")]
    public Transform endPoint;

    [Header("Patrol Look Around / 순찰 중 주위 살피기")]
    [Min(0f)]
    [Tooltip("각 순찰 지점에 도착한 뒤 주위를 살피는 시간입니다. 추적 중 타깃을 놓쳤을 때의 Lost Target Wait Time과 별개입니다. 회전 각도는 추적 스크립트의 Look Around Angle을 공유합니다.")]
    public float patrolLookAroundTime = 3f;
    [Min(0.05f)]
    [Tooltip("순찰 지점에서 멈추는 거리입니다. 전투용 정지 거리와 별도로 적용합니다.")]
    public float arrivalDistance = 0.2f;

    private EnemyMovement movement;
    private bool movingToEnd;
    private bool hasMoveRequest;
    private Vector3 requestedPoint;
    private bool isLookingAround;
    private float lookAroundTimer;
    private Vector3 lookAroundStartDirection;
    private bool warnedInvalidRoute;

    public bool IsLookingAround => isLookingAround;
    public float LookAroundTimeRemaining => lookAroundTimer;
    public Transform CurrentPatrolPoint => movingToEnd ? endPoint : startPoint;

    public void Initialize(EnemyMovement enemyMovement)
    {
        movement = enemyMovement;
    }

    // 不使用独立 Update，避免巡逻与观察、追逐同时向移动模块下命令。
    // 별도 Update를 사용하지 않아 순찰과 관찰, 추적이 동시에 이동 명령을 내리지 않게 한다.
    public void Tick(float navMeshSampleRange, float lookAroundAngle, float deltaTime)
    {
        if (!isActiveAndEnabled || movement == null || movement.IsTraversingLink)
        {
            return;
        }

        if (!HasValidRoute())
        {
            Interrupt();
            movement.Stop();
            movement.SetAutoRotation(false);
            return;
        }

        if (!movement.IsReady)
        {
            hasMoveRequest = false;
            return;
        }

        if (isLookingAround)
        {
            lookAroundTimer = patrolLookAroundTime <= 0f ? 0f :
                Mathf.Max(0f, lookAroundTimer - Mathf.Max(0f, deltaTime));
            float progress = patrolLookAroundTime <= 0f ? 1f : 1f - lookAroundTimer / patrolLookAroundTime;
            movement.LookAround(lookAroundStartDirection, progress, lookAroundAngle);
            if (lookAroundTimer <= 0f)
            {
                isLookingAround = false;
                movingToEnd = !movingToEnd;
                hasMoveRequest = false;
            }
            return;
        }

        Vector3 destination = CurrentPatrolPoint.position;
        if (!hasMoveRequest || (destination - requestedPoint).sqrMagnitude > 0.001f ||
            (!movement.Agent.hasPath && !movement.Agent.pathPending && !movement.HasReachedCompleteDestination))
        {
            hasMoveRequest = movement.TryMoveToPosition(destination, navMeshSampleRange, Mathf.Max(0.05f, arrivalDistance));
            requestedPoint = destination;
            if (!hasMoveRequest)
            {
                movement.Stop();
            }
            // 新请求至少等待一帧，不能用追逐留下的旧路径判断已经到达。
            // 새 요청은 최소 한 프레임 기다려 추적 중 남은 이전 경로로 도착 여부를 판단하지 않는다.
            return;
        }

        if (movement.HasReachedCompleteDestination)
        {
            isLookingAround = true;
            lookAroundTimer = Mathf.Max(0f, patrolLookAroundTime);
            lookAroundStartDirection = transform.forward;
            movement.Stop();
            movement.SetAutoRotation(false);
        }
    }

    // 感知或追逐打断时保留当前巡逻目标；恢复后重新走到该点再环顾。
    // 감지나 추적으로 중단되어도 현재 순찰 목표를 유지한다. 재개하면 해당 지점에 도착한 뒤 다시 주위를 살핀다.
    public void Interrupt()
    {
        hasMoveRequest = false;
        isLookingAround = false;
        lookAroundTimer = 0f;
    }

    bool HasValidRoute()
    {
        bool valid = startPoint != null && endPoint != null &&
            !startPoint.IsChildOf(transform) && !endPoint.IsChildOf(transform) &&
            (startPoint.position - endPoint.position).sqrMagnitude > 0.001f;
        if (!valid && !warnedInvalidRoute)
        {
            Debug.LogWarning("순찰하려면 서로 다른 위치의 Start Point와 End Point를 지정하세요. 두 지점은 적의 자식일 수 없습니다.", this);
        }
        warnedInvalidRoute = !valid;
        return valid;
    }

    void OnDisable()
    {
        if (movement != null && (hasMoveRequest || isLookingAround))
        {
            movement.Stop();
        }
        Interrupt();
    }

    void OnValidate()
    {
        patrolLookAroundTime = Mathf.Max(0f, patrolLookAroundTime);
        arrivalDistance = Mathf.Max(0.05f, arrivalDistance);
    }
}
