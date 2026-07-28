using UnityEngine;

// 敌人目标工具类：提供“到目标表面的距离”“目标表面最近点”“目标整体 Bounds”这类通用计算。
// 적 타깃 유틸리티 클래스: "타깃 표면까지의 거리", "타깃 표면의 가장 가까운 점", "타깃 전체 Bounds" 같은 공통 계산을 제공한다.
// static 表示不需要挂到 GameObject 上，可以直接 EnemyTargetUtility.函数名 调用。
// static은 GameObject에 붙일 필요 없이 EnemyTargetUtility.함수명 으로 바로 호출할 수 있다는 뜻이다.

public static class EnemyTargetUtility
{
    //我离目标表面多远？
    public static float GetDistanceToTarget(Vector3 sourcePoint, GameObject target)
    {
        // 没有目标时返回无限大，表示“距离非常远，不应该被优先选择”。
        // 타깃이 없으면 무한대를 반환한다. "매우 멀어서 우선 선택되면 안 된다"는 의미다.
        if (target == null)
        {
            return Mathf.Infinity;
        }

        // 获取目标自己以及所有子物体上的 Collider。
        // 타깃 자신과 모든 자식 오브젝트의 Collider를 가져온다.
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
            {
                continue;
            }

            // ClosestPoint 会返回这个 Collider 表面上离 sourcePoint 最近的点。
            // ClosestPoint는 이 Collider 표면에서 sourcePoint와 가장 가까운 점을 반환한다.
            Vector3 closestPoint = col.ClosestPoint(sourcePoint);
            // 算 sourcePoint 到这个最近点的距离。
            // sourcePoint에서 이 가장 가까운 점까지의 거리를 계산한다.
            float distance = Vector3.Distance(sourcePoint, closestPoint);

            // 保留目前找到的最短距离。
            // 현재까지 찾은 가장 짧은 거리를 저장한다.
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        // 如果目标有 Collider，就返回到 Collider 表面的最近距离。
        // 타깃에 Collider가 있으면 Collider 표면까지의 가장 가까운 거리를 반환한다.
        if (closestDistance != Mathf.Infinity)
        {
            return closestDistance;
        }

        // 如果没有任何 Collider，就退回到中心点距离。
        // Collider가 하나도 없으면 중심점까지의 거리로 대체한다.
        return Vector3.Distance(sourcePoint, target.transform.position);
    }

    //目标表面离我最近的点在哪里？
    public static Vector3 GetClosestPointToTarget(Vector3 sourcePoint, GameObject target)
    {
        // 没有目标时，直接返回 sourcePoint，避免空引用。
        // 타깃이 없으면 sourcePoint를 그대로 반환해서 null 참조를 피한다.
        if (target == null)
        {
            return sourcePoint;
        }

        // 查找目标所有 Collider，方便处理复杂模型或者子物体碰撞体。
        // 복잡한 모델이나 자식 오브젝트 Collider를 처리하기 위해 모든 Collider를 찾는다.
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        Vector3 closestPoint = target.transform.position;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
            {
                continue;
            }

            // 找到这个 Collider 表面离 sourcePoint 最近的点。
            // 이 Collider 표면에서 sourcePoint와 가장 가까운 점을 찾는다.
            Vector3 point = col.ClosestPoint(sourcePoint);
            float distance = Vector3.Distance(sourcePoint, point);

            // 谁更近，就记录谁。
            // 더 가까운 점을 기록한다.
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    //目标整体有多大？
    public static bool TryGetTargetBounds(GameObject target, out Bounds bounds)
    {
        // 默认先给一个空 Bounds，中心在目标 transform.position。
        // 기본값으로 target.transform.position을 중심으로 하는 빈 Bounds를 만든다.
        bounds = new Bounds(target.transform.position, Vector3.zero);

        // Bounds 主要靠 Collider 来计算，因为寻路和攻击判断更关心碰撞体体积。
        // Bounds는 주로 Collider로 계산한다. 경로와 공격 판정은 실제 충돌체 크기가 더 중요하기 때문이다.
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        bool hasBounds = false;

        // 第一轮：优先使用非 Trigger Collider。
        // 1차: Trigger가 아닌 Collider를 우선 사용한다.
        // 原因：普通 Collider 通常代表真实阻挡体积，Trigger 通常只是检测范围。
        // 이유: 일반 Collider는 보통 실제 막는 부피이고, Trigger는 보통 감지 범위다.
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null || col.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                // 第一个有效 Collider 直接作为初始 Bounds。
                // 첫 번째 유효 Collider를 초기 Bounds로 사용한다.
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                // 把新的 Collider.bounds 合并进总 Bounds。
                // 새 Collider.bounds를 전체 Bounds 안에 합친다.
                bounds.Encapsulate(col.bounds);
            }
        }

        // 如果已经用普通 Collider 算到了范围，就直接返回。
        // 일반 Collider로 범위를 구했으면 바로 반환한다.
        if (hasBounds)
        {
            return true;
        }

        // 第二轮：如果完全没有普通 Collider，再允许 Trigger 参与计算。
        // 2차: 일반 Collider가 하나도 없을 때만 Trigger도 계산에 포함한다.
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                // 第一个 Trigger Collider 作为初始 Bounds。
                // 첫 번째 Trigger Collider를 초기 Bounds로 사용한다.
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                // 继续合并剩余 Collider 的范围。
                // 나머지 Collider 범위를 계속 합친다.
                bounds.Encapsulate(col.bounds);
            }
        }

        // true 表示成功拿到了目标整体范围；false 表示目标没有任何 Collider。
        // true는 타깃 전체 범위를 얻었다는 뜻이고, false는 Collider가 하나도 없다는 뜻이다.
        return hasBounds;
    }
}
