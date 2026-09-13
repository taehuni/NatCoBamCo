using UnityEngine;

// 只判断目标是否可见；目标选择、警觉计时和移动由各自的模块处理。
// 대상이 보이는지만 판단한다. 대상 선택, 경계도 누적, 이동은 각각의 모듈에서 처리한다.
[DisallowMultipleComponent]
public class EnemyVision : MonoBehaviour
{
    [Header("Field Of View / 시야")]
    [Range(1f, 180f)]
    [Tooltip("수평 시야의 전체 각도입니다. 120이면 정면을 기준으로 좌우 각각 60도입니다.")]
    public float viewAngle = 120f;

    [Tooltip("선택 사항: 적의 머리에 있는 빈 오브젝트를 지정합니다. 위치만 사용하며, 시야 방향은 적 루트 오브젝트의 파란색 로컬 Z축을 따릅니다.")]
    public Transform eyePoint;

    [Min(0f)]
    [Tooltip("Eye Point를 지정하지 않았을 때 적 루트 위치에서 눈까지의 높이입니다. 월드 단위를 사용합니다.")]
    public float eyeHeight = 1.5f;

    [Tooltip("시야를 가리는 레이어입니다. 기본값은 Ignore Raycast를 제외한 모든 레이어이며, 적과 감지 대상 자신의 Collider는 자동으로 제외됩니다. 가림막에는 Is Trigger가 꺼진 Collider가 있어야 합니다.")]
    public LayerMask sightBlockLayers = Physics.DefaultRaycastLayers;

    public Vector3 EyePosition => eyePoint != null ? eyePoint.position : transform.position + Vector3.up * eyeHeight;

    public Vector3 ViewForward
    {
        get
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }
    }

    public bool CanSeeTarget(GameObject target, float range)
    {
        float visibleDistance;
        return CanSeeTarget(target, range, out visibleDistance);
    }

    // 同时返回实际看见的身体碰撞体距离，供警觉计时使用，避免交互 Trigger 影响远近判断。
    // 실제로 보이는 신체 Collider까지의 거리도 반환해 경계도 누적에 사용하며, 상호작용 Trigger가 거리 판단에 영향을 주지 않게 한다.
    public bool CanSeeTarget(GameObject target, float range, out float visibleDistance)
    {
        visibleDistance = Mathf.Infinity;
        if (!isActiveAndEnabled || target == null || !target.activeInHierarchy || range <= 0f)
        {
            return false;
        }

        // 检查有效实体碰撞体的中心；交互 Trigger 不作为身体上的可见点。
        // 활성화된 일반 Collider의 중심을 검사한다. 상호작용 Trigger는 신체의 가시 지점으로 사용하지 않는다.
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider body = colliders[i];
            if (!body.enabled || body.isTrigger)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, body.ClosestPoint(transform.position));
            if (distance > range)
            {
                continue;
            }

            Vector3 sightPoint = body.bounds.center;
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(sightPoint - EyePosition, Vector3.up);
            if (horizontalDirection.sqrMagnitude > 0.001f &&
                Vector3.Angle(ViewForward, horizontalDirection) > Mathf.Clamp(viewAngle, 1f, 180f) * 0.5f)
            {
                continue;
            }

            if (HasClearLineOfSight(target.transform, sightPoint))
            {
                visibleDistance = distance;
                return true;
            }
        }

        return false;
    }

    bool HasClearLineOfSight(Transform target, Vector3 sightPoint)
    {
        Vector3 origin = EyePosition;

        // 射线不会报告包住起点的碰撞体，额外防止眼睛嵌入墙内时看穿墙。
        // 레이 시작점을 감싼 Collider는 레이캐스트에 잡히지 않으므로, 눈이 벽 안에 들어간 경우를 별도로 검사한다.
        Collider[] touchingEyes = Physics.OverlapSphere(origin, 0.01f, sightBlockLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < touchingEyes.Length; i++)
        {
            if (IsSightBlocker(touchingEyes[i].transform, target))
            {
                return false;
            }
        }

        Vector3 direction = sightPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        // 检查整段视线，忽略自身后仍然检查后面的墙，不依赖 RaycastAll 的返回顺序。
        // 시선 전체를 검사해 자신을 제외한 뒤에도 뒤쪽 벽을 확인하며, RaycastAll 결과의 순서에 의존하지 않는다.
        RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance,
            sightBlockLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsSightBlocker(hits[i].transform, target))
            {
                return false;
            }
        }

        return true;
    }

    bool IsSightBlocker(Transform hit, Transform target)
    {
        return !hit.IsChildOf(transform) && !hit.IsChildOf(target);
    }

    public void DrawFieldOfView(float range)
    {
        DrawFieldOfView(range, Color.cyan);
    }

    // 只绘制水平视野示意；真正可见区域仍由距离、目标碰撞体和遮挡检测决定。
    // 수평 시야의 개략적인 범위만 그린다. 실제 가시 영역은 거리, 대상 Collider, 가림 검사로 결정된다.
    public void DrawFieldOfView(float range, Color color)
    {
        if (range <= 0f)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = color;
        Vector3 origin = EyePosition;
        float angle = Mathf.Clamp(viewAngle, 1f, 180f);
        Vector3 leftDirection = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * ViewForward;
        Vector3 previous = origin + leftDirection * range;

#if UNITY_EDITOR
        Color previousHandleColor = UnityEditor.Handles.color;
        UnityEditor.Handles.color = new Color(color.r, color.g, color.b, 0.06f);
        UnityEditor.Handles.DrawSolidArc(origin, Vector3.up, leftDirection, angle, range);
        UnityEditor.Handles.color = previousHandleColor;
#endif

        Gizmos.DrawSphere(origin, 0.06f);
        Gizmos.DrawLine(transform.position, origin);
        Gizmos.DrawLine(origin, previous);
        const int segments = 32;
        for (int i = 1; i <= segments; i++)
        {
            float stepAngle = -angle * 0.5f + angle * i / segments;
            Vector3 next = origin + Quaternion.AngleAxis(stepAngle, Vector3.up) * ViewForward * range;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        Gizmos.DrawLine(origin, previous);
        Gizmos.DrawLine(origin, origin + ViewForward * Mathf.Min(range, 1f));
        Gizmos.color = previousColor;
    }
}
