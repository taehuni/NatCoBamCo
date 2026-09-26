using UnityEngine;

// 只计算位置：progress 是 0～1 的进度，不操作 Agent，也不模拟 Rigidbody。
// 위치만 계산한다. progress는 0~1 진행률이며 Agent나 Rigidbody를 제어하지 않는다.
public static class EnemyLinkTrajectory
{
    public static Vector3 Jump(Vector3 start, Vector3 end, float progress, float height)
    {
        progress = Mathf.Clamp01(progress);
        Vector3 point = Vector3.Lerp(start, end, progress);
        point.y += 4f * height * progress * (1f - progress);
        return point;
    }

    public static Vector3 Drop(Vector3 start, Vector3 end, float progress)
    {
        progress = Mathf.Clamp01(progress);
        Vector3 point = Vector3.Lerp(start, end, progress);
        // 水平匀速靠近落点，高度用平方进度，下降由慢到快。
        // 수평으로 착지점에 다가가고 높이에는 제곱 진행률을 사용해 점점 빠르게 내려간다.
        point.y = Mathf.Lerp(start.y, end.y, progress * progress);
        return point;
    }
}
