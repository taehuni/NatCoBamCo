using System.Collections.Generic;
using UnityEngine;

// 保存目标周围由设计者手动放置的寻路候选点。
// 디자이너가 목표 주변에 직접 배치한 경로 후보 지점을 저장한다.
public class TargetPathPoints : MonoBehaviour
{
    [Header("수동 경로 후보 지점")]
    public Transform[] pathPoints;

    // 读取所有有效候选点的世界坐标，空引用不会加入结果。
    // 유효한 모든 후보 지점의 월드 좌표를 읽으며, 빈 참조는 결과에서 제외한다.
    public bool TryGetWorldPoints(out Vector3[] worldPoints)
    {
        List<Vector3> validPoints = new List<Vector3>();

        if (pathPoints != null)
        {
            for (int i = 0; i < pathPoints.Length; i++)
            {
                if (pathPoints[i] == null)
                {
                    continue;
                }

                // Transform.position 是候选空对象在场景中的世界坐标。
                // Transform.position은 후보 빈 오브젝트의 월드 좌표이다.
                validPoints.Add(pathPoints[i].position);
            }
        }

        worldPoints = validPoints.ToArray();
        return worldPoints.Length > 0;
    }
}
