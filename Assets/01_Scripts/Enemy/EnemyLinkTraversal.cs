using UnityEngine;
using UnityEngine.AI;

// NavMesh 选路，本脚本只执行连接上的动作；不修改 Area Mask 或 Cost。
// 경로는 NavMesh가 선택하고 이 스크립트는 Link 동작만 실행한다. Area Mask와 Cost는 변경하지 않는다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(EnemyMovement))]
public sealed class EnemyLinkTraversal : MonoBehaviour
{
    [Header("Jump / 도약")]
    [Min(0f), Tooltip("JumpAcross 연결의 추가 도약 높이입니다. DropDown에는 적용하지 않습니다.")]
    public float jumpHeight = 1f;
    [Min(0.1f), Tooltip("JumpAcross 연결을 건너는 시간입니다. 단위: 초.")]
    public float jumpDuration = 1f;

    [Header("Drop / 낙하")]
    [Min(0.1f), Tooltip("낙하 거리로 이동 시간을 계산하는 기준 속도입니다. 하강은 느리게 시작해 빨라집니다. Rigidbody 중력은 사용하지 않습니다.")]
    public float dropSpeed = 4f;

    [Header("Body And Environment / 몸과 환경")]
    [Tooltip("적의 몸 Collider를 지정하세요. 수직으로 선 Box Collider는 실제 크기와 회전으로 검사합니다. 비워두면 활성화된 비 Trigger Collider를 사용하며, 여러 개이거나 다른 형태이면 전체 외곽 범위를 사용합니다.")]
    public Collider bodyCollider;
    [Tooltip("지면, 플랫폼, 벽을 포함하세요. Enemy, Player, Trigger와 자신의 Collider는 제외합니다.")]
    public LayerMask environmentLayers = Physics.DefaultRaycastLayers;
    [Range(0.005f, 0.05f), Tooltip("바닥 접촉을 충돌로 오인하지 않도록 하는 작은 여유 값입니다.")]
    public float collisionSkin = 0.02f;
    [Range(0.05f, 0.4f), Tooltip("NavMesh와 실제 지면의 높이 차이를 허용하는 범위입니다. 층 사이 거리보다 작게 유지하세요.")]
    public float landingTolerance = 0.2f;
    public bool showTrajectoryGizmos = true;
    [UnityEngine.Serialization.FormerlySerializedAs("showEntranceOverlapDebug")]
    [Tooltip("Link 입구와 플랫폼 이탈 검사 실패 시 대상 Collider와 검사 위치를 출력합니다. 최대 5초에 한 번 출력하며, 선택한 적의 검사 상자는 빨간색으로 표시합니다.")]
    public bool showLinkDebug = true;

    private enum Phase { Idle, WalkingOut, Jumping, Dropping, Recovering }
    private Phase phase;
    private NavMeshAgent agent;
    private EnemyStatusEffects statusEffects;
    private Vector3 start, fallStart, end, destination, bodyOffset, bodyHalf, lastPosition;
    private Quaternion bodyRotation = Quaternion.identity;
    private float elapsed, duration, timeDebt, retryAt, recoverySpeed;
    private bool isDrop, hasDestination, savedPosition, savedRotation, savedStopped, savedAutoTraverse, ownsAgent;
    private bool hasPreview;
    private string lastWarning;
    private float nextDebugTime;
    private bool hasFailedCheck;
    private Bounds failedCheckBounds;
    private Quaternion failedCheckRotation;
    private Collider[] checkedObstacles;

    public bool IsTraversing => phase != Phase.Idle;
    public bool IsControllingMovement => IsTraversing || (isActiveAndEnabled && AgentReady &&
        !agent.isStopped && agent.isOnOffMeshLink && Time.time >= retryAt);
    public int EnvironmentMask => environmentLayers.value & ~LayerMask.GetMask("Enemy", "Player");
    private bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;
    private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        statusEffects = GetComponent<EnemyStatusEffects>();
    }

    private void OnEnable()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) return;
        if (!ownsAgent) { savedAutoTraverse = agent.autoTraverseOffMeshLink; ownsAgent = true; }
        agent.autoTraverseOffMeshLink = false;
        // 对象池若把敌人放到新位置，从新站位恢复，不继续旧轨迹。
        // 풀에서 위치를 바꿨다면 이전 궤적을 이어가지 않고 새 위치에서 복구한다.
        if (IsTraversing && (transform.position - lastPosition).sqrMagnitude > 0.01f)
        {
            CaptureBody();
            phase = Phase.Recovering;
            recoverySpeed = 0f;
        }
    }

    private void Update()
    {
        if (agent == null || !agent.enabled) return;
        if (!IsTraversing)
        {
            if (!AgentReady || !agent.isOnOffMeshLink || agent.isStopped || Time.time < retryAt ||
                (statusEffects != null && statusEffects.IsParalyzed)) return;
            BeginTraversal();
        }
        if (!IsTraversing) return;
        // 小步移动与扫掠检查，低帧率也不能一帧跨过平台或薄墙。
        // 작은 이동과 스윕 검사로 낮은 프레임에서도 플랫폼이나 얇은 벽을 건너뛰지 않게 한다.
        timeDebt += Time.deltaTime;
        for (int i = 0; i < 128 && timeDebt > 0f && IsTraversing; i++)
        {
            float dt = Mathf.Min(timeDebt, 0.01f);
            timeDebt -= dt;
            if (phase == Phase.WalkingOut) WalkOut(dt);
            else if (phase == Phase.Recovering) Recover(dt);
            else MoveCurve(dt);
        }
        lastPosition = transform.position;
    }

    private void BeginTraversal()
    {
        OffMeshLinkData link = agent.currentOffMeshLinkData;
        if (!link.valid) return;
        start = transform.position;
        CaptureBody();
        hasPreview = false;
        hasFailedCheck = false;
        if (EnvironmentMask == 0) { Reject("Environment Layers contains no usable ground or wall layers."); return; }
        if (!PoseClear(start, "Entrance/Body")) { Reject("The enemy body overlaps the environment at the Link entrance."); return; }
        if (!TryLanding(link.endPos, out end)) { Reject("No clear landing near the Link end. Check ground colliders and Base Offset."); return; }

        // 自动连接按 linkType 分类；现有手动连接按是否向下兼容，不引入梯子逻辑。
        // 자동 Link는 linkType으로 구분한다. 기존 수동 Link는 하강 여부로 처리하며 사다리 동작은 추가하지 않는다.
        switch (link.linkType)
        {
            case OffMeshLinkType.LinkTypeDropDown: isDrop = true; break;
            case OffMeshLinkType.LinkTypeJumpAcross: isDrop = false; break;
            default: isDrop = end.y < start.y - landingTolerance; break;
        }
        fallStart = start;
        if (isDrop)
        {
            if (end.y >= start.y) { Reject("A DropDown Link must lead to a lower landing."); return; }
            if (!FindPlatformExit()) { Reject("Cannot leave the platform before the Link end. Check body size, walls and landing clearance."); return; }
        }
        if (!CurveClear()) { Reject("The body would hit an obstacle along this Link. Check the landing or Jump Height."); return; }

        hasDestination = agent.hasPath && !float.IsInfinity(agent.destination.sqrMagnitude) && !float.IsNaN(agent.destination.sqrMagnitude);
        destination = agent.destination;
        savedPosition = agent.updatePosition;
        savedRotation = agent.updateRotation;
        savedStopped = agent.isStopped;
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        phase = isDrop ? Phase.WalkingOut : Phase.Jumping;
        duration = isDrop ? Mathf.Max(0.1f, Vector3.Distance(fallStart, end) / Mathf.Max(0.1f, dropSpeed)) : Mathf.Max(0.1f, jumpDuration);
        elapsed = timeDebt = recoverySpeed = 0f;
        hasPreview = true;
        lastWarning = null;
    }

    // 用脚底薄盒子沿水平方向试探，找到整个身体离开平台的位置。
    // 발밑의 얇은 상자를 수평으로 옮겨 몸 전체가 플랫폼을 벗어나는 위치를 찾는다.
    private bool FindPlatformExit()
    {
        Vector3 aboveEnd = new Vector3(end.x, start.y, end.z);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, aboveEnd) / 0.05f));
        Vector3 previous = start;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(start, aboveEnd, (float)i / steps);
            if (!SegmentClear(previous, point, "PlatformExit")) return false;
            if (!HasPlatformBelow(point, i == steps)) { fallStart = point; return true; }
            previous = point;
        }
        return false; // 到终点上方仍有平台，不能强行下降。 / 끝점 위에도 플랫폼이 있으면 강제로 내려가지 않는다.
    }

    private Vector3 CurvePoint(float progress) => isDrop
        ? EnemyLinkTrajectory.Drop(fallStart, end, progress)
        : EnemyLinkTrajectory.Jump(start, end, progress, jumpHeight);

    private bool CurveClear()
    {
        Vector3 previous = isDrop ? fallStart : start;
        int steps = Mathf.Max(8, Mathf.CeilToInt((Vector3.Distance(previous, end) + (isDrop ? 0f : jumpHeight * 2f)) / 0.05f));
        for (int i = 1; i <= steps; i++)
        {
            Vector3 next = CurvePoint((float)i / steps);
            if (!SegmentClear(previous, next)) return false;
            previous = next;
        }
        return true;
    }

    private void WalkOut(float dt)
    {
        // 平台变化时也按当前脚底判断，已经离地就不能继续悬空行走或被麻痹定住。
        // 플랫폼이 바뀌어도 현재 발밑을 확인한다. 이미 떠났으면 공중 보행이나 마비 정지를 하지 않는다.
        if (!HasPlatformBelow(transform.position))
        {
            fallStart = transform.position;
            duration = Mathf.Max(0.1f, Vector3.Distance(fallStart, end) / Mathf.Max(0.1f, dropSpeed));
            phase = Phase.Dropping;
            return;
        }
        if (statusEffects != null && statusEffects.IsParalyzed) return;
        Vector3 next = Vector3.MoveTowards(transform.position, fallStart, Mathf.Max(0.1f, agent.speed) * dt);
        if (!MoveBody(next)) { BeginRecovery("The way off the platform became blocked."); return; }
        if ((next - fallStart).sqrMagnitude > 0.000001f) return;
        if (HasPlatformBelow(next)) { BeginRecovery("The platform still supports the body. Drop cancelled."); return; }
        phase = Phase.Dropping;
    }

    private void MoveCurve(float dt)
    {
        elapsed = Mathf.Min(duration, elapsed + dt);
        if (!MoveBody(CurvePoint(elapsed / duration))) { BeginRecovery("An obstacle interrupted the Link movement."); return; }
        if (elapsed < duration) return;
        // 真正到达落点才完成连接，不能在失败时直接 Complete 或 ResetPath。
        // 실제 착지한 뒤에만 연결을 완료한다. 실패 시 Complete나 ResetPath로 끝점에 이동시키지 않는다.
        if (!AgentReady || !agent.isOnOffMeshLink || !agent.currentOffMeshLinkData.valid ||
            (agent.currentOffMeshLinkData.endPos - (end - Vector3.up * agent.baseOffset)).sqrMagnitude > landingTolerance * landingTolerance ||
            !TryLanding(end - Vector3.up * agent.baseOffset, out Vector3 landing) || !SegmentClear(transform.position, landing))
        { BeginRecovery("The Link or landing changed during traversal."); return; }
        transform.position = landing;
        agent.CompleteOffMeshLink();
        agent.nextPosition = landing;
        RestoreMovement();
    }

    private void BeginRecovery(string reason)
    {
        WarnOnce(reason);
        phase = Phase.Recovering;
        recoverySpeed = 0f;
    }

    // 途中受阻只尝试落到当前脚下的地面，不穿墙回起点或瞬移到终点。
    // 도중에 막히면 현재 발밑 지면으로만 복구한다. 벽을 뚫고 시작점이나 끝점으로 순간이동하지 않는다.
    private void Recover(float dt)
    {
        Vector3 feet = transform.position + bodyOffset - Vector3.up * bodyHalf.y;
        if (GroundRay(feet + Vector3.up * landingTolerance, landingTolerance * 2f, out RaycastHit hit) &&
            TryLanding(hit.point, out Vector3 landing) && SegmentClear(transform.position, landing) && agent.Warp(landing))
        {
            if (agent.isOnOffMeshLink) return;
            transform.position = landing;
            agent.ResetPath();
            RestoreMovement();
            retryAt = Time.time + 1f;
            if (hasDestination && AgentReady) agent.SetDestination(destination);
            return;
        }
        recoverySpeed += 9.81f * dt;
        if (!MoveBody(transform.position + Vector3.down * recoverySpeed * dt)) recoverySpeed = 0f;
    }

    private void Reject(string reason)
    {
        WarnOnce(reason);
        retryAt = Time.time + 1f;
        // 尚未移动，留在入口重试；不修改整类连接的通行权限或重置路径。
        // 아직 움직이지 않았으므로 입구에서 재시도한다. 연결 전체의 통행 권한이나 경로는 바꾸지 않는다.
    }

    private void RestoreMovement()
    {
        phase = Phase.Idle;
        timeDebt = 0f;
        agent.updatePosition = savedPosition;
        agent.updateRotation = savedRotation;
        if (AgentReady) agent.isStopped = savedStopped || (statusEffects != null && statusEffects.IsParalyzed);
    }

    private void CaptureBody()
    {
        bodyRotation = Quaternion.identity;
        Bounds bounds = new Bounds(transform.position + Vector3.up * (agent.height * 0.5f - agent.baseOffset),
            new Vector3(agent.radius * 2f, agent.height, agent.radius * 2f));
        Collider singleBody = null;
        int bodyCount = 0;
        foreach (Collider body in GetComponentsInChildren<Collider>())
        {
            if (!body.enabled || body.isTrigger || (bodyCollider != null && body != bodyCollider)) continue;
            if (bodyCount == 0) bounds = body.bounds;
            else bounds.Encapsulate(body.bounds);
            singleBody = body;
            bodyCount++;
        }
        // 直立 Box 使用真实尺寸和朝向，避免转身后的世界外包盒把身体算大。
        // 수직 Box는 실제 크기와 회전을 사용해 회전 후 월드 외곽 상자가 몸보다 커지는 것을 방지한다.
        if (bodyCount == 1 && singleBody is BoxCollider box && Mathf.Abs(box.transform.up.y) > 0.9999f)
        {
            Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
            bodyHalf = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;
            bodyOffset = box.transform.TransformPoint(box.center) - transform.position;
            bodyRotation = box.transform.rotation;
            return;
        }
        // 多碰撞体或倾斜身体继续使用保守的整体外框。
        // 여러 Collider 또는 기울어진 몸은 보수적인 전체 외곽 상자를 계속 사용한다.
        bodyOffset = bounds.center - transform.position;
        bodyHalf = bounds.extents;
    }

    // 保存检测盒的世界中心与自身尺寸，使用时还需传入 bodyRotation。
    // 검사 상자의 월드 중심과 자체 크기를 저장하며 사용할 때 bodyRotation도 전달한다.
    private Bounds GetFootCheckBox(Vector3 position)
    {
        float depth = landingTolerance + collisionSkin * 2f;
        Vector3 feet = position + bodyOffset - Vector3.up * (bodyHalf.y + depth * 0.5f - collisionSkin);
        Vector3 half = new Vector3(bodyHalf.x + collisionSkin, depth * 0.5f, bodyHalf.z + collisionSkin);
        return new Bounds(feet, half * 2f);
    }

    private bool HasPlatformBelow(Vector3 position, bool debugAtSearchEnd = false)
    {
        Bounds bounds = GetFootCheckBox(position);
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, bodyRotation, EnvironmentMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (debugAtSearchEnd && showLinkDebug)
                LogLinkCheckFailure(position, hits, "PlatformExit/StillSupportedAtEnd", true);
            return true;
        }
        return false;
    }

    private Vector3 CollisionHalf => new Vector3(Mathf.Max(0.01f, bodyHalf.x - collisionSkin),
        Mathf.Max(0.01f, bodyHalf.y - collisionSkin), Mathf.Max(0.01f, bodyHalf.z - collisionSkin));

    private bool PoseClear(Vector3 position, string debugStage = null)
    {
        Collider[] hits = Physics.OverlapBox(position + bodyOffset, CollisionHalf, bodyRotation,
            EnvironmentMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (debugStage != null && showLinkDebug) LogLinkCheckFailure(position, hits, debugStage);
            return false;
        }
        return true;
    }

    // 复用原检查结果，区分身体受阻与脚底仍有支撑；预检查位置不等于敌人实际位置。
    // 기존 검사 결과로 몸의 충돌과 발밑 지지를 구분한다. 사전 검사 위치는 적의 실제 위치와 다르다.
    private void LogLinkCheckFailure(Vector3 position, Collider[] hits, string stage, bool footCheck = false)
    {
        hasFailedCheck = true;
        failedCheckBounds = footCheck ? GetFootCheckBox(position) : new Bounds(position + bodyOffset, CollisionHalf * 2f);
        failedCheckRotation = bodyRotation;
        checkedObstacles = hits;
        if (Time.unscaledTime < nextDebugTime) return;
        nextDebugTime = Time.unscaledTime + 5f;

        OffMeshLinkData link = agent.currentOffMeshLinkData;
        Vector3 aboveEnd = new Vector3(end.x, start.y, end.z);
        string details = $"Enemy='{GetHierarchyPath(transform)}', actual position={transform.position:F3}, rotation={transform.eulerAngles:F1}\n" +
            $"Test position={position:F3} (preview only; enemy is not moved), check={(footCheck ? "Foot support" : "Body")}\n" +
            $"Link={link.linkType}, start={link.startPos:F3}, end={link.endPos:F3}\n" +
            (stage.StartsWith("PlatformExit") ? $"Exit search: from={start:F3}, to={aboveEnd:F3}, horizontal distance={Vector3.Distance(start, aboveEnd):F3}\n" : "") +
            $"Check box center={failedCheckBounds.center:F3}, size={failedCheckBounds.size:F3}, rotation={failedCheckRotation.eulerAngles:F1}\n" +
            $"Body feet Y at test={position.y + bodyOffset.y - bodyHalf.y:F4}, check bottom Y={failedCheckBounds.min.y:F4}\n" +
            $"Agent radius={agent.radius:F3}, height={agent.height:F3}, base offset={agent.baseOffset:F3}, skin={collisionSkin:F3}";

        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            string overlap = footCheck
                ? "Foot support remains at the search end. This is not a body penetration test."
                : "Exact body overlap: not checked. Assign an enabled child Body Collider for comparison.";
            if (!footCheck && bodyCollider != null && bodyCollider.enabled && !bodyCollider.isTrigger && bodyCollider.transform.IsChildOf(transform))
            {
                if (SupportsPenetration(bodyCollider) || SupportsPenetration(hit))
                {
                    Vector3 testBodyPosition = bodyCollider.transform.position + position - transform.position;
                    bool penetrating = Physics.ComputePenetration(bodyCollider, testBodyPosition,
                        bodyCollider.transform.rotation, hit, hit.transform.position, hit.transform.rotation,
                        out Vector3 direction, out float depth);
                    overlap = penetrating
                        ? $"Exact body overlap: YES. Separation distance={depth:F4} m, direction={direction:F3}"
                        : "Exact body overlap: not reported. Possible touching or bounds-only contact; mesh backfaces are ignored by this test.";
                }
                else overlap = "Exact body overlap: not checked for this collider type pair.";
                overlap += $"\nBody='{GetHierarchyPath(bodyCollider.transform)}' ({bodyCollider.GetType().Name}), bounds size={bodyCollider.bounds.size:F3}";
            }

            // Console 中点击此日志可定位具体阻挡物，层级路径用于区分同名 Cube。
            // Console에서 이 로그를 클릭하면 해당 장애물을 찾을 수 있다. 계층 경로로 같은 이름의 Cube를 구분한다.
            Debug.LogWarning($"[Enemy Link Debug] Stage={stage}, Hit='{GetHierarchyPath(hit.transform)}' ({hit.GetType().Name})\n" +
                $"Layer='{LayerMask.LayerToName(hit.gameObject.layer)}' ({hit.gameObject.layer}), ID={hit.GetInstanceID()}, " +
                $"bounds min={hit.bounds.min:F3}, max={hit.bounds.max:F3}\n{overlap}\n{details}", hit);
        }
    }

    private static bool SupportsPenetration(Collider collider) => collider is BoxCollider || collider is SphereCollider ||
        collider is CapsuleCollider || (collider is MeshCollider mesh && mesh.convex);

    private static string GetHierarchyPath(Transform item)
    {
        string path = item.name;
        while (item.parent != null) { item = item.parent; path = item.name + "/" + path; }
        return path;
    }

    private bool SegmentClear(Vector3 from, Vector3 to, string debugStage = null)
    {
        string bodyStage = debugStage != null ? debugStage + "/BodyBlocked" : null;
        if (!PoseClear(from, bodyStage) || !PoseClear(to, bodyStage)) return false;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.0000001f) return true;
        foreach (RaycastHit hit in Physics.BoxCastAll(from + bodyOffset, CollisionHalf, delta.normalized,
            bodyRotation, delta.magnitude, EnvironmentMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (debugStage != null && showLinkDebug)
                LogLinkCheckFailure(from + delta.normalized * hit.distance, new[] { hit.collider },
                    $"{debugStage}/SweepBlocked; hit point={hit.point:F3}, normal={hit.normal:F3}, distance={hit.distance:F4}");
            return false;
        }
        return true;
    }

    private bool MoveBody(Vector3 next)
    {
        if (!SegmentClear(transform.position, next)) return false;
        transform.position = next;
        return true;
    }

    private bool GroundRay(Vector3 origin, float distance, out RaycastHit ground)
    {
        ground = default;
        float closest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, distance, EnvironmentMask, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.distance < closest) { ground = hit; closest = hit.distance; }
        return ground.collider != null && ground.normal.y >= Mathf.Cos(NavMesh.GetSettingsByID(agent.agentTypeID).agentSlope * Mathf.Deg2Rad);
    }

    private bool TryLanding(Vector3 requested, out Vector3 landing)
    {
        landing = default;
        if (!NavMesh.SamplePosition(requested, out NavMeshHit nav, landingTolerance, Filter) ||
            !GroundRay(nav.position + Vector3.up * landingTolerance, landingTolerance * 2f, out RaycastHit ground)) return false;
        landing = nav.position + Vector3.up * agent.baseOffset;
        float feetY = landing.y + bodyOffset.y - bodyHalf.y;
        return feetY >= ground.point.y - collisionSkin && feetY <= ground.point.y + landingTolerance && PoseClear(landing);
    }

    private void OnDisable()
    {
        if (agent == null) return;
        // 空中禁用则暂停并保留位置控制，重新启用后继续，避免 Agent 将身体拉回起点。
        // 공중 비활성화 시 위치 제어를 유지하고 재활성화 후 계속해 Agent가 몸을 시작점으로 당기지 않게 한다.
        if (IsTraversing) { lastPosition = transform.position; return; }
        if (ownsAgent) agent.autoTraverseOffMeshLink = savedAutoTraverse;
        ownsAgent = false;
    }

    private void WarnOnce(string message)
    {
        if (lastWarning == message) return;
        lastWarning = message;
        Debug.LogWarning("Enemy Link Traversal: " + message, this);
    }

    private void OnValidate()
    {
        jumpHeight = Mathf.Max(0f, jumpHeight);
        jumpDuration = Mathf.Max(0.1f, jumpDuration);
        dropSpeed = Mathf.Max(0.1f, dropSpeed);
        collisionSkin = Mathf.Clamp(collisionSkin, 0.005f, 0.05f);
        landingTolerance = Mathf.Clamp(landingTolerance, 0.05f, 0.4f);
    }

    private void OnDrawGizmosSelected()
    {
        // 在失败的预检查位置显示身体盒或脚底盒，不把它误画在敌人当前站位。
        // 실패한 사전 검사 위치에 몸 또는 발밑 상자를 표시하며 적의 현재 위치와 혼동하지 않게 한다.
        if (Application.isPlaying && showLinkDebug && hasFailedCheck && AgentReady && agent.isOnOffMeshLink)
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = Color.red;
            DrawCheckBox(failedCheckBounds, failedCheckRotation);
            Gizmos.DrawLine(transform.position + bodyOffset, failedCheckBounds.center);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            foreach (Collider hit in checkedObstacles)
                if (hit != null && !hit.transform.IsChildOf(transform)) Gizmos.DrawWireCube(hit.bounds.center, hit.bounds.size);
            Gizmos.color = previousColor;
        }
        if (!showTrajectoryGizmos) return;
        // 绿色显示 NavMesh 选的路线，便于区分选路绕行与连接动作问题。
        // 초록색으로 NavMesh 경로를 표시해 우회 경로와 Link 동작 문제를 구분한다.
        if (AgentReady && agent.hasPath)
        {
            Gizmos.color = Color.green;
            Vector3[] corners = agent.path.corners;
            for (int i = 1; i < corners.Length; i++) Gizmos.DrawLine(corners[i - 1], corners[i]);
        }
        if (!hasPreview) return;
        Gizmos.color = isDrop ? Color.cyan : Color.yellow;
        if (isDrop) Gizmos.DrawLine(start, fallStart);
        Vector3 previous = isDrop ? fallStart : start;
        for (int i = 1; i <= 32; i++)
        {
            Vector3 next = CurvePoint(i / 32f);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        DrawCheckBox(new Bounds(transform.position + bodyOffset, bodyHalf * 2f), bodyRotation);
    }

    private static void DrawCheckBox(Bounds box, Quaternion rotation)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(box.center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, box.size);
        Gizmos.matrix = previousMatrix;
    }
}
