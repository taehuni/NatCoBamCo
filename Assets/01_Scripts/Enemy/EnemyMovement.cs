using UnityEngine;
using UnityEngine.AI;

// 敌人移动模块：统一封装 NavMeshAgent 的移动、停止、转向、路径计算。
// 적 이동 모듈: NavMeshAgent의 이동, 정지, 회전, 경로 계산을 통합해서 감싼다.
// 其他脚本不直接操作 NavMeshAgent，优先通过 EnemyMovement 调用，结构会更清晰。
// 다른 스크립트는 NavMeshAgent를 직접 조작하기보다 EnemyMovement를 통해 호출하는 것이 구조적으로 더 명확하다.

// 敌人移动模块：统一封装 NavMeshAgent 的移动、停止、转向、路径计算。
// 적 이동 모듈: NavMeshAgent의 이동, 정지, 회전, 경로 계산을 한곳에 모아 관리함.
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Data / 이동 데이터")]
    public float moveSpeed;

    private NavMeshAgent agent;
    private float defaultStoppingDistance;

    // 缓存 NavMeshAgent 引用。第一次访问时 GetComponent，之后直接复用。
    // NavMeshAgent 참조를 캐싱. 처음 접근할 때 GetComponent를 하고 이후에는 재사용.
    public NavMeshAgent Agent
    {
        get
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            return agent;
        }
    }

    // 判断 Agent 是否可以正常寻路：组件存在、启用、并且敌人站在 NavMesh 上。
    // Agent가 정상적으로 길찾기를 할 수 있는지 확인: 컴포넌트 존재, 활성화, NavMesh 위에 있음.
    public bool IsReady
    {
        get
        {
            return Agent != null && Agent.enabled && Agent.isOnNavMesh;
        }
    }

    // NavMeshAgent 的半径，表示敌人在寻路系统里大概占多大空间。
    // NavMeshAgent의 반지름. 길찾기 시스템에서 적이 차지하는 대략적인 공간 크기.
    public float Radius
    {
        get
        {
            if (Agent == null)
            {
                return 0f;
            }

            return Agent.radius;
        }
    }

    // 当前停止距离，敌人距离目标多近时认为可以停下。
    // 현재 정지 거리. 목표에 얼마나 가까워지면 멈출지 나타냄.
    public float StoppingDistance
    {
        get
        {
            if (Agent == null)
            {
                return 0f;
            }

            return Agent.stoppingDistance;
        }
    }

    // 初始化移动数据：同步基础速度、记录默认停止距离、设置避障优先级和自动转向。
    // 이동 데이터 초기화: 기본 속도 동기화, 기본 정지 거리 기록, 회피 우선순위와 자동 회전 설정.
    public void Initialize(EnemyAI enemyAI)
    {
        if (Agent == null)
        {
            return;
        }

        Agent.speed = moveSpeed;
        defaultStoppingDistance = Agent.stoppingDistance;
        // 障碍避让优先级，数值越小优先级越高。随机化可以减少敌人完全重叠排队。
        // 장애물 회피 우선순위. 숫자가 작을수록 우선순위가 높음. 랜덤화하면 적들이 완전히 겹치는 현상을 줄일 수 있음.
        Agent.avoidancePriority = Random.Range(30, 71);
        SetAutoRotation(true);
    }

    // 使用默认 stoppingDistance 移动到目标位置。
    // 기본 stoppingDistance를 사용해서 목표 위치로 이동.
    public void MoveToPosition(Vector3 position, float navMeshSampleRange)
    {
        MoveToPosition(position, navMeshSampleRange, defaultStoppingDistance);
    }

    // 移动到目标位置：先把目标点修正到 NavMesh 上，再 SetDestination。
    // 목표 위치로 이동: 먼저 목표점을 NavMesh 위의 점으로 보정한 뒤 SetDestination을 호출.
    public void MoveToPosition(Vector3 position, float navMeshSampleRange, float stoppingDistance)
    {
        if (!IsReady)
        {
            return;
        }

        Vector3 navMeshPosition;

        // 目标点可能不在 NavMesh 上，所以先找附近最近的可走点。
        // 목표점이 NavMesh 위에 없을 수 있으므로 먼저 주변의 가장 가까운 이동 가능 지점을 찾음.
        if (!TryGetNavMeshPoint(position, navMeshSampleRange, out navMeshPosition))
        {
            return;
        }

        // 设置停止距离、恢复自动转向、恢复移动，然后真正发出移动命令。
        // 정지 거리 설정, 자동 회전 복구, 이동 재개 후 실제 이동 명령을 내림.
        Agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        SetAutoRotation(true);
        Agent.isStopped = false;
        Agent.SetDestination(navMeshPosition);
    }

    // 停止移动，但不销毁 Agent，也不清空目标数据。
    // 이동을 멈춤. Agent를 제거하거나 목표 데이터를 지우지는 않음.
    public void Stop()
    {
        if (!IsReady)
        {
            return;
        }

        Agent.isStopped = true;
    }

    // 是否让 NavMeshAgent 自动控制敌人朝向。
    // NavMeshAgent가 적의 회전을 자동으로 제어할지 설정.
    public void SetAutoRotation(bool useAutoRotation)
    {
        if (Agent == null)
        {
            return;
        }

        Agent.updateRotation = useAutoRotation;
    }

    // 逐渐转向某个点，适合攻击时让敌人面对目标。
    // 특정 지점을 향해 천천히 회전. 공격 시 적이 목표를 바라보게 할 때 사용.
    public void FacePoint(Vector3 point, float turnSpeed)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        // 方向太短说明目标几乎和自己重合，不需要转向。
        // 방향이 너무 짧으면 목표가 거의 자기 위치와 같으므로 회전할 필요 없음.
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        // LookRotation 根据方向生成目标旋转。
        // LookRotation은 방향을 기준으로 목표 회전값을 생성.
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        // RotateTowards 按 turnSpeed * deltaTime 逐渐转过去。
        // RotateTowards는 turnSpeed * deltaTime 만큼 점진적으로 회전시킴.
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    // 瞬间转向某个点，不做平滑旋转。
    // 특정 지점을 즉시 바라봄. 부드러운 회전은 하지 않음.
    public void FacePointInstant(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    // 在 sourcePoint 周围 sampleRange 范围内寻找最近的 NavMesh 可走点。
    // sourcePoint 주변 sampleRange 범위 안에서 가장 가까운 NavMesh 이동 가능 지점을 찾음.
    public bool TryGetNavMeshPoint(Vector3 sourcePoint, float sampleRange, out Vector3 navMeshPoint)
    {
        NavMeshHit hit;

        // NavMesh.SamplePosition 是 Unity 的固定函数，只负责找附近可走点，不负责计算完整路径。
        // NavMesh.SamplePosition은 Unity 기본 함수. 주변 이동 가능 지점을 찾을 뿐, 전체 경로를 계산하지 않음.
        if (NavMesh.SamplePosition(sourcePoint, out hit, sampleRange, NavMesh.AllAreas))
        {
            navMeshPoint = hit.position;
            return true;
        }

        navMeshPoint = sourcePoint;
        return false;
    }

    // 简化版 TryGetPath：不需要外部拿到 navMeshDestination 时使用。
    // 간단 버전 TryGetPath: 외부에서 navMeshDestination이 필요 없을 때 사용.
    public bool TryGetPath(
        Vector3 destination,
        float navMeshSampleRange,
        out NavMeshPath path,
        out Vector3 lastReachablePoint)
    {
        Vector3 navMeshDestination;
        return TryGetPath(destination, navMeshSampleRange, out path, out lastReachablePoint, out navMeshDestination);
    }

    // 计算从敌人当前位置到目标点的 NavMesh 路径。
    // 적 현재 위치에서 목표 지점까지의 NavMesh 경로를 계산.
    public bool TryGetPath(
        Vector3 destination,
        float navMeshSampleRange,
        out NavMeshPath path,
        out Vector3 lastReachablePoint,
        out Vector3 navMeshDestination)
    {
        // 先准备默认结果，保证即使中途失败 out 参数也有值。
        // 먼저 기본 결과를 준비해서 중간에 실패해도 out 파라미터가 값을 가지게 함.
        path = new NavMeshPath();
        lastReachablePoint = transform.position;
        navMeshDestination = destination;

        if (!IsReady)
        {
            return false;
        }

        // 先把目标点修正到 NavMesh 上。
        // 먼저 목표점을 NavMesh 위의 위치로 보정.
        if (!TryGetNavMeshPoint(destination, navMeshSampleRange, out navMeshDestination))
        {
            return false;
        }

        // CalculatePath 是 Unity 的固定函数：只计算路径，不会让敌人移动。
        // CalculatePath는 Unity 기본 함수. 경로만 계산하고 적을 이동시키지는 않음.
        bool hasPath = Agent.CalculatePath(navMeshDestination, path);

        // corners 的最后一个点，就是这条路径最后能到达的位置。
        // corners의 마지막 점은 이 경로에서 마지막으로 도달 가능한 위치.
        if (path.corners != null && path.corners.Length > 0)
        {
            lastReachablePoint = path.corners[path.corners.Length - 1];
        }

        return hasPath;
    }

    // 计算 NavMeshPath 的实际路线长度：把相邻拐点之间的距离全部加起来。
    // NavMeshPath의 실제 경로 길이 계산: 인접한 코너 사이의 거리를 모두 더함.
    public float GetPathLength(NavMeshPath path)
    {
        // 少于两个点时无法组成线段，所以返回 0。
        // 점이 2개보다 적으면 선분을 만들 수 없으므로 0 반환.
        if (path == null || path.corners == null || path.corners.Length < 2)
        {
            return 0f;
        }

        float length = 0f;

        // 从第 1 个点开始，每次拿当前点和上一个点计算距离。
        // 1번 인덱스부터 시작해서 현재 점과 이전 점 사이의 거리를 계산.
        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return length;
    }
}
