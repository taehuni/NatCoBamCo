using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyTargetSelector))]
[RequireComponent(typeof(EnemyVision))]
[RequireComponent(typeof(EnemyPatrolBehaviour))]
[RequireComponent(typeof(EnemyLinkTraversal))]
public class EnemyPlayerChaseBehaviour : MonoBehaviour
{
    // 资源地区的日常行为类型，与普通、高速、坦克等战斗种类分开。
    // 자원 지역의 평상시 행동 유형으로, 일반·고속·탱크 등의 전투 종류와 구분한다.
    public enum ResourceEnemyType
    {
        Stationary,
        Patrol
    }

    [Header("Resource Area Behaviour / 자원 지역 행동")]
    [Tooltip("Stationary: 평소 제자리에 서 있고 추적이 끝나면 최초 위치로 돌아갑니다. Patrol: Enemy Patrol Behaviour의 두 지점을 왕복하며, 추적이 끝나면 중단 당시 향하던 지점으로 복귀합니다.")]
    public ResourceEnemyType enemyType = ResourceEnemyType.Stationary;
    [Min(0.05f)]
    [Tooltip("Stationary 유형이 최초 위치로 돌아올 때 멈추는 거리입니다. 순찰 도착 거리는 Enemy Patrol Behaviour에서 설정합니다.")]
    public float returnArrivalDistance = 0.2f;

    [Header("Player Detection / 플레이어 감지")]
    [Tooltip("플레이어를 처음 감지할 수 있는 최대 거리입니다. 거리별 감지 속도 계산의 상한값으로도 사용합니다.")]
    public float findRange = 8f; // 感知范围 / 감지 범위
    [Tooltip("추적을 시작한 뒤 이 거리 밖으로 나가면 타깃을 해제합니다. 적과 플레이어 중심 사이의 3D 거리이며, 방향과 벽의 가림은 무시합니다.")]
    public float losePlayerRange = 12f; // 丢失范围 / 추적 해제 범위
    [Min(0f)]
    [Tooltip("Find Range 거리에서 감지 진행률이 0%에서 100%가 될 때까지 걸리는 시간입니다. 플레이어가 가까울수록 더 빠르게 누적됩니다.")]
    public float detectionTime = 2f;
    [Min(0f)]
    [Tooltip("플레이어가 시야 안에 보이고 거리가 이 값 이하이면 즉시 추적합니다. Find Range를 넘을 수 없으며, 거리는 적 위치에서 보이는 신체 Collider 표면까지 측정합니다.")]
    public float instantDetectionRange = 5f;
    public float searchInterval = 0.1f; // 感知间隔 / 감지 간격
    public LayerMask playerLayer; // 玩家图层 / 플레이어 레이어

    [Header("Observe And Wait / 관찰 및 대기")]
    [Min(0f)]
    [Tooltip("플레이어를 처음 발견했을 때 제자리에서 바라보는 회전 속도입니다. 단위는 초당 각도입니다.")]
    public float lookTurnSpeed = 180f;
    [Min(0f)]
    [Tooltip("추적을 시작하기 전에 플레이어를 놓쳤을 때 마지막으로 바라보던 방향을 유지하는 시간입니다. 이 동안에도 플레이어 감지는 계속합니다.")]
    public float lookHoldDuration = 1.5f;
    [Range(0f, 180f)]
    [Tooltip("순찰 지점에서 주위를 살필 때 도착 당시의 방향을 기준으로 좌우로 회전하는 최대 각도입니다.")]
    public float lookAroundAngle = 120f;

    [Header("Movement / 이동")]
    public float navMeshSampleRange = 5f; // 把目标点修正到附近 NavMesh 的范围 / 목표 지점을 근처 NavMesh로 보정하는 범위

    [Header("Detection Gizmos / 감지 범위 표시")]
    [Tooltip("시야 범위와 눈 위치를 표시합니다. Scene 뷰의 Gizmos도 켜야 합니다.")]
    public bool showDetectionGizmos = true;
    [Tooltip("켜면 선택한 적만 표시하고, 끄면 선택하지 않은 적도 표시합니다.")]
    public bool drawGizmosOnlyWhenSelected = false;
    [Tooltip("추적 해제 거리를 적 중심의 노란색 구로 표시합니다. 청록색 부채꼴은 최초 감지 범위, 녹색 부채꼴은 즉시 추적 범위입니다.")]
    public bool showChaseRangeGizmo = true;

    private EnemyAI enemyAI;
    private EnemyMovement movement;
    private EnemyTargetSelector targetSelector;
    private EnemyVision vision;
    private EnemyPatrolBehaviour patrol;
    private Vector3 initialPosition;
    private bool isReturningToInitialPosition;
    private bool hasReturnMoveRequest;

    private GameObject targetPlayer; // 锁定的目标 / 고정된 타겟
    private GameObject detectedPlayer; // 当前感知到的玩家 / 현재 감지된 플레이어
    private float detectionTimer; // 感知计时器 / 감지 타이머
    private float detectedPlayerDistance = Mathf.Infinity;
    private float nextSearchTime; // 下次感知时间 / 다음 감지 시간
    private float lookHoldTimer;
    private bool isChasing;
    private Vector3 chasePosition;

    public bool IsChasing => isChasing;
    public bool IsHoldingDirection => !isChasing && detectedPlayer == null && lookHoldTimer > 0f;
    public bool IsReturningToInitialPosition => isReturningToInitialPosition;

    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        movement = GetOrAddComponent<EnemyMovement>();
        targetSelector = GetOrAddComponent<EnemyTargetSelector>();
        // 兼容已经挂有此行为的旧对象，移动模块已由 EnemyAI.Awake 初始化。
        // 이 행동이 이미 부착된 기존 오브젝트도 지원한다. 이동 모듈은 EnemyAI.Awake에서 초기화된다.
        vision = GetOrAddComponent<EnemyVision>();
        patrol = GetOrAddComponent<EnemyPatrolBehaviour>();
        GetOrAddComponent<EnemyLinkTraversal>();
        patrol.Initialize(movement);
        initialPosition = transform.position;
    }

    void Update()
    {
        if (enemyAI == null || movement == null || targetSelector == null || vision == null)
        {
            return;
        }

        if (enemyAI.IsParalyzed() || movement.IsTraversingLink)
        {
            return;
        }

        if (isChasing)
        {
            HandlePlayerChase();
            return;
        }

        UpdateDetectedPlayer();
        HandlePlayerDetection();

        // 观察和追逐优先；没有目标且朝向停留结束后，才允许返回或巡逻。
        // 관찰과 추적을 우선한다. 타깃이 없고 방향 유지 시간이 끝난 뒤에만 복귀나 순찰을 허용한다.
        if (!isChasing && detectedPlayer == null && !IsHoldingDirection)
        {
            HandleIdleBehaviour();
        }
    }

    // 按固定间隔刷新当前感知到的玩家
    // 일정 간격으로 현재 감지된 플레이어를 갱신
    void UpdateDetectedPlayer()
    {
        // 已发现的玩家每帧验证视线，防止搜索间隔内躲到墙后仍继续积累警觉。
        // 이미 발견한 플레이어의 시야를 매 프레임 확인해 탐색 간격 중 벽 뒤에 숨으면 경계도가 누적되지 않게 한다.
        if (detectedPlayer != null && !vision.CanSeeTarget(detectedPlayer, findRange, out detectedPlayerDistance))
        {
            detectedPlayer = null;
        }

        if (Time.time < nextSearchTime)
        {
            return;
        }

        if (detectedPlayer == null)
        {
            detectedPlayer = targetSelector.FindVisiblePlayerTarget(playerLayer, findRange, vision);
            if (detectedPlayer != null && !vision.CanSeeTarget(detectedPlayer, findRange, out detectedPlayerDistance))
            {
                detectedPlayer = null;
            }
        }
        nextSearchTime = Time.time + Mathf.Max(0.01f, searchInterval);
    }

    // 看得见时原地观察并累计警觉；看不见时逐渐降低警觉，保持最后朝向一小会。
    // 보이면 제자리에서 관찰하며 경계도를 누적하고, 보이지 않으면 경계도를 낮추면서 마지막 방향을 잠시 유지한다.
    void HandlePlayerDetection()
    {
        if (detectedPlayer != null)
        {
            if (patrol != null)
            {
                patrol.Interrupt();
            }
            hasReturnMoveRequest = false;
            StopAndHoldDirection();
            movement.FacePoint(detectedPlayer.transform.position, Mathf.Max(0f, lookTurnSpeed));
            lookHoldTimer = Mathf.Max(0f, lookHoldDuration);
            AccumulateDetection(detectedPlayerDistance, Time.deltaTime);

            if (detectionTimer >= detectionTime)
            {
                targetPlayer = detectedPlayer;
                chasePosition = transform.position;
                detectionTimer = 0f;
                lookHoldTimer = 0f;
                isChasing = true;
                isReturningToInitialPosition = false;
                HandlePlayerChase();
            }
        }
        else
        {
            detectionTimer = Mathf.Max(0f, detectionTimer - Time.deltaTime);
            lookHoldTimer = Mathf.Max(0f, lookHoldTimer - Time.deltaTime);
            if (lookHoldTimer > 0f)
            {
                StopAndHoldDirection();
            }
        }
    }

    // 统一分配空闲时的移动控制权，巡逻组件本身不运行 Update。
    // 평상시 이동 제어권을 한곳에서 배분하며, 순찰 컴포넌트 자체는 Update를 실행하지 않는다.
    void HandleIdleBehaviour()
    {
        if (enemyType == ResourceEnemyType.Patrol)
        {
            isReturningToInitialPosition = false;
            hasReturnMoveRequest = false;
            if (patrol != null && patrol.isActiveAndEnabled)
            {
                patrol.Tick(navMeshSampleRange, lookAroundAngle, Time.deltaTime);
            }
            else
            {
                StopAndHoldDirection();
            }
            return;
        }

        if (patrol != null)
        {
            patrol.Interrupt();
        }
        if (isReturningToInitialPosition)
        {
            ReturnToInitialPosition();
        }
        else
        {
            StopAndHoldDirection();
        }
    }

    // 返回开始运行时记录的站位，途中仍由 Update 优先检查玩家感知。
    // 실행을 시작할 때 기록한 위치로 돌아간다. 복귀 중에도 Update에서 플레이어 감지를 우선 확인한다.
    void ReturnToInitialPosition()
    {
        if (!movement.IsReady)
        {
            hasReturnMoveRequest = false;
            return;
        }

        if (!hasReturnMoveRequest)
        {
            hasReturnMoveRequest = movement.TryMoveToPosition(initialPosition, navMeshSampleRange,
                Mathf.Max(0.05f, returnArrivalDistance));
            if (!hasReturnMoveRequest)
            {
                StopAndHoldDirection();
            }
            return;
        }

        if (movement.HasReachedCompleteDestination)
        {
            isReturningToInitialPosition = false;
            hasReturnMoveRequest = false;
            StopAndHoldDirection();
        }
    }

    // 只改变累计速度，不因玩家移动而重置已经累计的警觉。
    // 누적 속도만 변경하며, 플레이어가 이동해도 이미 쌓인 경계도는 초기화하지 않는다.
    void AccumulateDetection(float distance, float deltaTime)
    {
        float maxDetectionTime = Mathf.Max(0f, detectionTime);
        float requiredTime = GetDetectionDuration(distance);
        if (requiredTime <= 0f)
        {
            detectionTimer = maxDetectionTime;
            return;
        }

        float accumulationRate = maxDetectionTime / requiredTime;
        detectionTimer = Mathf.Min(maxDetectionTime,
            detectionTimer + Mathf.Max(0f, deltaTime) * accumulationRate);
    }

    // 例：上限 8m、下限 5m、最远耗时 2s，则 6.5m 处从零累计需要 1s。
    // 예: 상한 8m, 하한 5m, 최대 소요 시간 2초이면 6.5m에서는 0부터 누적하는 데 1초가 걸린다.
    float GetDetectionDuration(float distance)
    {
        float farRange = Mathf.Max(0.1f, findRange);
        float nearRange = Mathf.Clamp(instantDetectionRange, 0f, farRange);
        float distanceRatio = Mathf.InverseLerp(nearRange, farRange, distance);
        return Mathf.Max(0f, detectionTime) * distanceRatio;
    }

    // 锁定后只按距离决定是否继续追逐，不再检查视野方向和墙体遮挡。
    // 타깃 고정 후에는 거리만으로 추적 유지 여부를 판단하며 시야 방향과 벽의 가림은 확인하지 않는다.
    void HandlePlayerChase()
    {
        if (targetPlayer == null || !targetPlayer.activeInHierarchy)
        {
            ClearTargetPlayer();
            return;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
        if (distance > losePlayerRange)
        {
            ClearTargetPlayer();
            return;
        }

        // 持续更新玩家脚下的可走地面，跳下平台或躲到墙后也继续跟踪。
        // 플랫폼에서 뛰어내리거나 벽 뒤에 숨어도 플레이어 발밑의 이동 가능한 지면을 계속 갱신한다.
        if (movement.TryGetTargetGroundPosition(targetPlayer, navMeshSampleRange, out Vector3 ground))
        {
            chasePosition = ground;
        }
        // 暂时找不到有效落点时继续走向上一次有效地面，下帧重新检查。
        // 유효한 착지점을 잠시 찾지 못하면 이전 지면으로 이동하며 다음 프레임에 다시 확인한다.
        movement.MoveToPosition(chasePosition, navMeshSampleRange);
    }

    // 停止位移并关闭 NavMesh 的自动转向；不再转向已经不可见的玩家。
    // 이동과 NavMesh 자동 회전을 멈추고, 더 이상 보이지 않는 플레이어를 향해 회전하지 않는다.
    void StopAndHoldDirection()
    {
        movement.Stop();
        movement.SetAutoRotation(false);
    }

    void ClearTargetPlayer()
    {
        // 清除追逐后，静止类型回原站位；巡逻目标由巡逻组件保留。
        // 추적을 정리한 뒤 정지형은 원래 위치로 복귀하며, 순찰 목표는 순찰 컴포넌트가 유지한다.
        if (isChasing && enemyType == ResourceEnemyType.Stationary)
        {
            isReturningToInitialPosition = true;
        }
        hasReturnMoveRequest = false;
        if (patrol != null)
        {
            patrol.Interrupt();
        }
        targetPlayer = null;
        detectedPlayer = null;
        detectionTimer = 0f;
        detectedPlayerDistance = Mathf.Infinity;
        nextSearchTime = 0f;
        lookHoldTimer = 0f;
        isChasing = false;
        if (movement != null)
        {
            StopAndHoldDirection();
        }
    }

    void OnDisable()
    {
        ClearTargetPlayer();
    }

    void OnValidate()
    {
        findRange = Mathf.Max(0.1f, findRange);
        instantDetectionRange = Mathf.Clamp(instantDetectionRange, 0f, findRange);
        losePlayerRange = Mathf.Max(findRange, losePlayerRange);
        detectionTime = Mathf.Max(0f, detectionTime);
        searchInterval = Mathf.Max(0.01f, searchInterval);
        navMeshSampleRange = Mathf.Max(0.1f, navMeshSampleRange);
        returnArrivalDistance = Mathf.Max(0.05f, returnArrivalDistance);
#if UNITY_EDITOR
        QueueRequiredComponentCheck();
#endif
    }

#if UNITY_EDITOR
    private bool requiredComponentCheckQueued;

    // 已存在的场景对象也自动补齐巡逻和跳落组件，延后处理以避开 OnValidate。
    // 기존 씬 오브젝트에도 순찰과 도약 컴포넌트를 자동 추가하며 OnValidate 밖에서 지연 처리한다.
    void QueueRequiredComponentCheck()
    {
        if (Application.isPlaying || requiredComponentCheckQueued)
        {
            return;
        }
        requiredComponentCheckQueued = true;
        UnityEditor.EditorApplication.delayCall += EnsureRequiredComponentsInEditor;
    }

    void EnsureRequiredComponentsInEditor()
    {
        if (this == null)
        {
            return;
        }
        requiredComponentCheckQueued = false;
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid())
        {
            return;
        }
        if (GetComponent<EnemyPatrolBehaviour>() == null)
        {
            UnityEditor.Undo.AddComponent<EnemyPatrolBehaviour>(gameObject);
        }
        if (GetComponent<EnemyLinkTraversal>() == null)
        {
            UnityEditor.Undo.AddComponent<EnemyLinkTraversal>(gameObject);
        }
    }
#endif

    void OnDrawGizmos()
    {
        if (!drawGizmosOnlyWhenSelected)
        {
            DrawDetectionGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (drawGizmosOnlyWhenSelected)
        {
            DrawDetectionGizmos();
        }
    }

    void DrawDetectionGizmos()
    {
        if (!showDetectionGizmos)
        {
            return;
        }

        EnemyVision currentVision = vision != null ? vision : GetComponent<EnemyVision>();
        if (currentVision == null)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        if (showChaseRangeGizmo)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, losePlayerRange);
        }
        currentVision.DrawFieldOfView(findRange, Color.cyan);
        float nearRange = Mathf.Clamp(instantDetectionRange, 0f, Mathf.Max(0f, findRange));
        if (nearRange > 0f)
        {
            currentVision.DrawFieldOfView(nearRange, Color.green);
        }

        if (Application.isPlaying && isChasing)
        {
            // 标记当前追逐的地面位置，锁定后允许持续跟踪墙后的玩家。
            // 현재 추적 중인 지면 위치를 표시한다. 타깃 고정 후에는 벽 뒤 플레이어도 계속 추적한다.
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(chasePosition, 0.2f);
            Gizmos.DrawLine(transform.position, chasePosition);
        }
        Gizmos.color = previousColor;

#if UNITY_EDITOR
        Vector3 origin = currentVision.EyePosition;
        Vector3 forward = currentVision.ViewForward;
        UnityEditor.Handles.Label(origin + Vector3.up * 0.2f, "Eyes");
        UnityEditor.Handles.Label(origin + forward * findRange,
            $"Detection: {findRange:0.#} / {currentVision.viewAngle:0.#} deg");
        if (nearRange > 0f)
        {
            UnityEditor.Handles.Label(origin + forward * nearRange,
                $"Instant detection: {nearRange:0.#}");
        }
        if (showChaseRangeGizmo)
        {
            UnityEditor.Handles.Label(transform.position + forward * losePlayerRange,
                $"Chase radius: {losePlayerRange:0.#}");
        }

        if (Application.isPlaying)
        {
            // 计时器现在代表可加速积累的警觉值，百分比比“已经过去几秒”更准确。
            // 타이머는 가속 누적되는 경계도이므로 실제 경과 시간보다 백분율로 표시하는 편이 정확하다.
            float detectionPercent = detectionTime > 0f ? Mathf.Clamp01(detectionTimer / detectionTime) * 100f : 0f;
            string state;
            if (enemyAI != null && enemyAI.IsParalyzed())
                state = "Paralyzed";
            else if (isChasing)
                state = "Chasing";
            else if (IsHoldingDirection)
                state = $"Hold: {lookHoldTimer:0.0}s | Detection: {detectionPercent:0}%";
            else if (detectedPlayer != null)
                state = $"Observing | Detection: {detectionPercent:0}%";
            else if (isReturningToInitialPosition)
                state = "Return to initial position";
            else if (enemyType == ResourceEnemyType.Patrol && patrol != null && patrol.isActiveAndEnabled)
                state = patrol.IsLookingAround ? $"Patrol look around: {patrol.LookAroundTimeRemaining:0.0}s" : "Patrolling";
            else
                state = $"Detection: {detectionPercent:0}%";

            UnityEditor.Handles.Label(origin + Vector3.up * 0.6f, state);
        }
#endif
    }

    T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();

        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}
