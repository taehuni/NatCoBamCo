using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace BamCoNatCo.EditorTools
{
    // 独立编辑器工具：Shift+T 启用，保留 Unity 默认的 T 矩形工具。
    // 독립 에디터 도구: Shift+T로 활성화하며 Unity의 기본 T 사각형 도구를 유지한다.
    [EditorTool("Snap Resize")]
    public sealed class SnapResizeTool : EditorTool
    {
        private const string MenuPath = "Tools/Snap Resize Tool";
        private const string UndoName = "Snap Resize";
        private const float SnapRadius = 20f;
        private static readonly int HandleHash = "BamCoNatCo.SnapResize".GetHashCode();
        private static readonly Rect PanelRect = new Rect(12f, 12f, 350f, 94f);
        private readonly GUIContent icon = new GUIContent("Snap", "单边吸附拉伸 / Snap Resize (Shift+T)");

        private Transform dragTarget;
        private SceneView dragView;
        private Bounds dragBounds;
        private SnapResizeGeometry.Snapshot snapshot;
        private Vector2 startMouse;
        private int activeControl;
        private int undoGroup = -1;
        private bool vertexKeyHeld;
        private bool alwaysSnap;
        private bool snapped;
        private Vector3 snappedVertex;
        private Transform[] snapCandidates;
        private Transform[] snapIgnored;

        public override GUIContent toolbarIcon => icon;

        [MenuItem(MenuPath)]
        private static void ActivateFromMenu()
        {
            ToolManager.SetActiveTool<SnapResizeTool>();
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [Shortcut("BamCoNatCo/Snap Resize Tool", typeof(SceneView), KeyCode.T, ShortcutModifiers.Shift)]
        private static void ActivateShortcut()
        {
            if (ValidateMenu())
                ActivateFromMenu();
        }

        public override void OnActivated()
        {
            SceneView.beforeSceneGui += TrackVertexKey;
            Selection.selectionChanged += OnSelectionChanged;
            vertexKeyHeld = false;
        }

        public override void OnWillBeDeactivated()
        {
            SceneView.beforeSceneGui -= TrackVertexKey;
            Selection.selectionChanged -= OnSelectionChanged;
            FinishDrag(false);
            vertexKeyHeld = false;
        }

        private void OnSelectionChanged()
        {
            FinishDrag(false);
            SceneView.RepaintAll();
        }

        private void TrackVertexKey(SceneView view)
        {
            Event evt = Event.current;
            // rawType 也能识别已经被 Unity 快捷键系统处理过的按键事件。
            // rawType으로 Unity 단축키 시스템이 처리한 키 이벤트도 확인한다.
            if (evt.keyCode == KeyCode.V &&
                (evt.rawType == EventType.KeyDown || evt.rawType == EventType.KeyUp))
            {
                vertexKeyHeld = evt.rawType == EventType.KeyDown;
                view.Repaint();
            }
            if (evt.rawType == EventType.MouseLeaveWindow && activeControl == 0)
                vertexKeyHeld = false;
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view))
                return;

            Event evt = Event.current;
            TrackVertexKey(view);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                FinishDrag(false);
                DrawPanel("请退出 Play 模式后编辑场景。", false);
                return;
            }

            Transform selected = Selection.activeTransform;
            bool valid = Selection.transforms.Length == 1 && selected != null &&
                !EditorUtility.IsPersistent(selected) && selected.gameObject.scene.IsValid() &&
                !SceneVisibilityManager.instance.IsHidden(selected.gameObject, true);
            Bounds bounds = default;
            valid = valid && SnapResizeGeometry.TryGetLocalBounds(selected, out bounds);
            if (!valid)
            {
                FinishDrag(false);
                DrawPanel("请选择一个 Cube、Plane 或带网格的场景对象。", false);
                return;
            }

            if (activeControl != 0 && (selected != dragTarget || dragTarget == null))
                FinishDrag(false);

            DrawPanel(activeControl == 0
                ? "拖动彩色圆点：单边拉伸；按住 V：吸附顶点。"
                : (snapped ? "已吸附：另一边保持不动。" : "将鼠标靠近目标顶点；Esc 取消本次拉伸。"), true);

            if (activeControl != 0 && view == dragView)
            {
                if (evt.rawType == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    FinishDrag(true);
                    if (evt.type != EventType.Used) evt.Use();
                    view.Repaint();
                    return;
                }
                if (evt.rawType == EventType.MouseUp && evt.button == 0)
                {
                    FinishDrag(false);
                    if (evt.type != EventType.Used) evt.Use();
                }
                else if (evt.rawType == EventType.MouseDrag || evt.rawType == EventType.KeyDown ||
                    evt.rawType == EventType.KeyUp)
                {
                    UpdateResize(evt.mousePosition);
                    if (evt.type == EventType.MouseDrag) evt.Use();
                    view.Repaint();
                }
            }

            DrawBounds(selected, activeControl != 0 ? dragBounds : bounds);
            DrawHandles(view, selected, bounds);
            if (activeControl != 0)
                DrawSnapFeedback(view);
        }

        private void DrawPanel(string message, bool allowOptions)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("单边吸附拉伸 / Snap Resize", EditorStyles.boldLabel);
            GUILayout.Label(message, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(!allowOptions))
                alwaysSnap = GUILayout.Toggle(alwaysSnap, "自动吸附（勾选后无需按住 V）");
            GUILayout.Label("Shift+T 启用 · T 返回矩形工具 · Ctrl+Z 撤销", EditorStyles.miniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void DrawBounds(Transform selected, Bounds bounds)
        {
            using (new Handles.DrawingScope(new Color(1f, 0.65f, 0.15f), selected.localToWorldMatrix))
                Handles.DrawWireCube(bounds.center, bounds.size);
        }

        private void DrawHandles(SceneView view, Transform selected, Bounds bounds)
        {
            Event evt = Event.current;
            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int id = GUIUtility.GetControlID(HandleHash + axis * 2 + (side + 1) / 2, FocusType.Passive);
                    if (!SnapResizeGeometry.Snapshot.TryCreate(selected, bounds, axis, side, out var handle))
                        continue;
                    Vector3 position = handle.movingWorldPoint;
                    // 从正上方看时，不显示朝向摄像机的深度手柄，以免与其他点重叠。
                    // 정면 방향의 깊이 핸들은 숨겨 다른 핸들과 겹치지 않게 한다.
                    Vector2 projectedDirection = HandleUtility.WorldToGUIPoint(
                        position + handle.direction * HandleUtility.GetHandleSize(position)) -
                        HandleUtility.WorldToGUIPoint(position);
                    if (projectedDirection.sqrMagnitude < 4f && id != activeControl)
                        continue;
                    if (view.camera.WorldToViewportPoint(position).z <= 0f)
                        continue;

                    float size = HandleUtility.GetHandleSize(position) * 0.07f;
                    Color color = axis == 0 ? Handles.xAxisColor : axis == 1 ? Handles.yAxisColor : Handles.zAxisColor;
                    if (id == activeControl || (activeControl == 0 && HandleUtility.nearestControl == id))
                        color = Handles.selectedColor;
                    using (new Handles.DrawingScope(color))
                    {
                        if (evt.type == EventType.Layout && !PanelRect.Contains(evt.mousePosition))
                            HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, size));
                        if (evt.type == EventType.Repaint)
                            Handles.DotHandleCap(id, position, Quaternion.identity, size, EventType.Repaint);
                    }

                    if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt &&
                        GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id &&
                        !PanelRect.Contains(evt.mousePosition))
                    {
                        BeginDrag(view, selected, bounds, handle, id, evt.mousePosition);
                        evt.Use();
                    }
                }
            }
        }

        private void BeginDrag(SceneView view, Transform selected, Bounds bounds,
            SnapResizeGeometry.Snapshot state, int control, Vector2 mouse)
        {
            dragTarget = selected;
            dragView = view;
            dragBounds = bounds;
            snapshot = state;
            activeControl = control;
            startMouse = mouse;
            snapped = false;
            snapCandidates = CollectSnapCandidates(selected, view.camera);
            snapIgnored = selected.GetComponentsInChildren<Transform>(true);
            GUIUtility.hotControl = control;
            GUIUtility.keyboardControl = 0;
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
        }

        private void UpdateResize(Vector2 mouse)
        {
            if (dragTarget == null)
                return;

            float translation = HandleUtility.CalcLineTranslation(startMouse, mouse,
                snapshot.movingWorldPoint, snapshot.direction);
            float factor = 1f + translation / snapshot.worldSize;
            snapped = false;
            if ((alwaysSnap || vertexKeyHeld) && snapCandidates != null && snapCandidates.Length > 0 &&
                HandleUtility.FindNearestVertex(mouse, snapCandidates, snapIgnored, out Vector3 vertex) &&
                (HandleUtility.WorldToGUIPoint(vertex) - mouse).sqrMagnitude <= SnapRadius * SnapRadius &&
                snapshot.TryGetSnapFactor(vertex, out float snapFactor))
            {
                factor = snapFactor;
                snappedVertex = vertex;
                snapped = true;
            }

            if (float.IsNaN(factor) || float.IsInfinity(factor))
                return;
            snapshot.Evaluate(factor, out Vector3 position, out Vector3 scale);
            if ((dragTarget.localPosition - position).sqrMagnitude < 0.0000000001f &&
                (dragTarget.localScale - scale).sqrMagnitude < 0.0000000001f)
                return;

            Undo.RecordObject(dragTarget, UndoName);
            dragTarget.localScale = scale;
            dragTarget.localPosition = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(dragTarget);
        }

        // 使用 Unity 自己的顶点拾取 API，不要求模型打开 Read/Write，也不修改共享网格。
        // Unity 정점 선택 API를 사용하므로 Read/Write 설정이나 공유 메시 수정이 필요 없다.
        internal static Transform[] CollectSnapCandidates(Transform selected, Camera camera)
        {
            var result = new List<Transform>();
            var stage = StageUtility.GetStageHandle(selected.gameObject);
            foreach (MeshFilter filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                GameObject obj = filter.gameObject;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled ||
                    !obj.activeInHierarchy || EditorUtility.IsPersistent(obj) || !obj.scene.IsValid() ||
                    filter.transform == selected || filter.transform.IsChildOf(selected) ||
                    StageUtility.GetStageHandle(obj) != stage ||
                    SceneVisibilityManager.instance.IsHidden(obj, true) ||
                    SceneVisibilityManager.instance.IsPickingDisabled(obj, true) ||
                    (camera != null && (camera.cullingMask & (1 << obj.layer)) == 0))
                    continue;
                result.Add(filter.transform);
            }
            return result.ToArray();
        }

        private void DrawSnapFeedback(SceneView view)
        {
            using (new Handles.DrawingScope(Color.yellow))
            {
                Handles.DrawWireDisc(snapshot.fixedWorldPoint, view.camera.transform.forward,
                    HandleUtility.GetHandleSize(snapshot.fixedWorldPoint) * 0.06f);
                Handles.Label(snapshot.fixedWorldPoint, "固定 / Fixed");
            }
            if (snapped && dragTarget != null)
            {
                using (new Handles.DrawingScope(Color.green))
                {
                    Vector3 center = dragBounds.center;
                    center[snapshot.axis] = snapshot.fixedLocalPoint[snapshot.axis] == dragBounds.min[snapshot.axis]
                        ? dragBounds.max[snapshot.axis] : dragBounds.min[snapshot.axis];
                    Handles.DrawWireDisc(snappedVertex, view.camera.transform.forward,
                        HandleUtility.GetHandleSize(snappedVertex) * 0.08f);
                    Handles.DrawDottedLine(dragTarget.TransformPoint(center), snappedVertex, 4f);
                }
            }
        }

        private void FinishDrag(bool cancel)
        {
            if (activeControl == 0)
                return;
            if (GUIUtility.hotControl == activeControl)
                GUIUtility.hotControl = 0;
            Undo.FlushUndoRecordObjects();
            if (undoGroup >= 0)
            {
                if (cancel)
                    Undo.RevertAllDownToGroup(undoGroup);
                else
                    Undo.CollapseUndoOperations(undoGroup);
            }
            activeControl = 0;
            undoGroup = -1;
            dragTarget = null;
            dragView = null;
            snapCandidates = null;
            snapIgnored = null;
            snapped = false;
        }
    }
}
