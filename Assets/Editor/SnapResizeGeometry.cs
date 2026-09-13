using UnityEditor;
using UnityEngine;

namespace BamCoNatCo.EditorTools
{
    // 拉伸计算与鼠标操作分开，所有尺寸都以选中对象的局部坐标为准。
    // 크기 계산과 마우스 처리를 분리하고 선택한 오브젝트의 로컬 좌표를 사용한다.
    internal static class SnapResizeGeometry
    {
        internal const float MinimumWorldSize = 0.001f;

        internal static bool TryGetLocalBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (target == null || target is RectTransform)
                return false;

            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled ||
                    !filter.gameObject.activeInHierarchy ||
                    SceneVisibilityManager.instance.IsHidden(filter.gameObject, true))
                    continue;

                // 先转换网格自己的边界，再合并，避免把世界轴对齐边界当成局部尺寸。
                // 메시 경계를 로컬 좌표로 변환한 뒤 합쳐서 회전된 오브젝트의 크기도 정확히 계산한다.
                Bounds meshBounds = filter.sharedMesh.bounds;
                Matrix4x4 toLocal = target.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = GetCorner(meshBounds, corner);
                    point = toLocal.MultiplyPoint3x4(point);
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return found;
        }

        internal static Vector3 GetCorner(Bounds bounds, int corner)
        {
            return new Vector3(
                (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
        }

        internal static Vector3 GetFaceCenter(Bounds bounds, int axis, int side)
        {
            Vector3 point = bounds.center;
            point[axis] += bounds.extents[axis] * side;
            return point;
        }

        // 保存拖动开始时的数据，每次都由原始状态计算，防止连续拖动产生累计误差。
        // 드래그 시작 상태를 저장하고 매번 원본으로 계산해 누적 오차를 방지한다.
        internal struct Snapshot
        {
            internal int axis;
            internal Vector3 fixedLocalPoint;
            internal Vector3 fixedWorldPoint;
            internal Vector3 movingWorldPoint;
            internal Vector3 direction;
            internal float worldSize;
            internal float minimumFactor;
            private float signedLocalSize;
            private Matrix4x4 worldToLocal;
            private Vector3 originalPosition;
            private Vector3 originalScale;
            private Quaternion originalRotation;

            internal static bool TryCreate(Transform target, Bounds bounds, int axis, int side, out Snapshot snapshot)
            {
                snapshot = default;
                if (target == null || axis < 0 || axis > 2 || (side != -1 && side != 1) ||
                    bounds.size[axis] < 0.000001f ||
                    Mathf.Abs(target.localToWorldMatrix.determinant) < 0.00000001f)
                    return false;

                Vector3 fixedLocal = GetFaceCenter(bounds, axis, -side);
                Vector3 movingLocal = GetFaceCenter(bounds, axis, side);
                Vector3 fixedWorld = target.TransformPoint(fixedLocal);
                Vector3 movingWorld = target.TransformPoint(movingLocal);
                float size = Vector3.Distance(fixedWorld, movingWorld);
                if (size < MinimumWorldSize)
                    return false;

                snapshot = new Snapshot
                {
                    axis = axis,
                    fixedLocalPoint = fixedLocal,
                    fixedWorldPoint = fixedWorld,
                    movingWorldPoint = movingWorld,
                    direction = (movingWorld - fixedWorld) / size,
                    worldSize = size,
                    minimumFactor = MinimumWorldSize / size,
                    signedLocalSize = movingLocal[axis] - fixedLocal[axis],
                    worldToLocal = target.worldToLocalMatrix,
                    originalPosition = target.localPosition,
                    originalScale = target.localScale,
                    originalRotation = target.localRotation
                };
                return true;
            }

            internal bool TryGetSnapFactor(Vector3 worldVertex, out float factor)
            {
                // 只取目标顶点在拉伸轴上的坐标，其他两轴不会跟着鼠标移动。
                // 대상 정점의 크기 조절 축 좌표만 사용하고 나머지 두 축은 움직이지 않는다.
                Vector3 localVertex = worldToLocal.MultiplyPoint3x4(worldVertex);
                factor = (localVertex[axis] - fixedLocalPoint[axis]) / signedLocalSize;
                return !float.IsNaN(factor) && !float.IsInfinity(factor) && factor >= minimumFactor;
            }

            internal void Evaluate(float factor, out Vector3 localPosition, out Vector3 localScale)
            {
                factor = Mathf.Max(minimumFactor, factor);
                localScale = originalScale;
                localScale[axis] *= factor;

                // 补偿轴心位移，使对面的整个边/面保持原位。
                // 피벗 이동을 보정해서 반대쪽 모서리/면 전체를 원래 위치에 고정한다.
                // 在父对象旋转、非均匀缩放或选中对象有负缩放时也使用相同公式。
                // 부모 회전, 비균일 스케일, 음수 스케일에도 같은 공식을 적용한다.
                localPosition = originalPosition + originalRotation *
                    Vector3.Scale(originalScale - localScale, fixedLocalPoint);
            }
        }
    }
}
