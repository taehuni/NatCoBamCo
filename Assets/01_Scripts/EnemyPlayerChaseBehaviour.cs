using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyTargetSelector))]
public class EnemyPlayerChaseBehaviour : MonoBehaviour
{
    [Header("Player Detection / 플레이어 감지")]
    public float findRange = 8f; // 感知范围 / 감지 범위
    public float losePlayerRange = 12f; // 丢失范围 / 추적 해제 범위
    public float detectionTime = 2f; // 玩家停留多久后开始追逐 / 플레이어가 감지 범위에 머문 뒤 추적 시작 시간
    public float searchInterval = 0.1f; // 感知间隔 / 감지 간격
    public LayerMask playerLayer; // 玩家图层 / 플레이어 레이어

    [Header("Movement / 이동")]
    public float navMeshSampleRange = 5f; // 把目标点修正到附近 NavMesh 的范围 / 목표 지점을 근처 NavMesh로 보정하는 범위

    private EnemyAI enemyAI;
    private EnemyMovement movement;
    private EnemyTargetSelector targetSelector;

    private GameObject targetPlayer; // 锁定的目标 / 고정된 타겟
    private GameObject detectedPlayer; // 当前感知到的玩家 / 현재 감지된 플레이어
    private float detectionTimer; // 感知计时器 / 감지 타이머
    private float nextSearchTime; // 下次感知时间 / 다음 감지 시간

    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        movement = GetOrAddComponent<EnemyMovement>();
        targetSelector = GetOrAddComponent<EnemyTargetSelector>();

        if (movement != null)
        {
            movement.Initialize(enemyAI);
        }
    }

    void Update()
    {
        if (enemyAI == null || movement == null || targetSelector == null)
        {
            return;
        }

        if (enemyAI.IsParalyzed())
        {
            return;
        }

        UpdateDetectedPlayer();
        HandlePlayerDetection();
    }

    // 按固定间隔刷新当前感知到的玩家
    // 일정 간격으로 현재 감지된 플레이어를 갱신
    void UpdateDetectedPlayer()
    {
        if (Time.time < nextSearchTime)
        {
            return;
        }

        detectedPlayer = targetSelector.FindPlayerTarget(playerLayer, findRange);
        nextSearchTime = Time.time + searchInterval;
    }

    // 玩家在感知范围内停留一段时间后锁定；超出丢失范围后解除锁定
    // 플레이어가 감지 범위에 일정 시간 머물면 고정하고, 추적 해제 범위를 벗어나면 해제
    void HandlePlayerDetection()
    {
        if (targetPlayer != null)
        {
            float distance = EnemyTargetUtility.GetDistanceToTarget(transform.position, targetPlayer);

            if (distance >= losePlayerRange)
            {
                ClearTargetPlayer();
                return;
            }

            movement.MoveToPosition(targetPlayer.transform.position, navMeshSampleRange);
            return;
        }

        if (detectedPlayer != null)
        {
            detectionTimer += Time.deltaTime;

            if (detectionTimer >= detectionTime)
            {
                targetPlayer = detectedPlayer;
                detectionTimer = 0f;
            }
        }
        else
        {
            detectionTimer -= Time.deltaTime;

            if (detectionTimer <= 0f)
            {
                detectionTimer = 0f;
            }
        }
    }

    void ClearTargetPlayer()
    {
        targetPlayer = null;
        detectedPlayer = null;
        detectionTimer = 0f;
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
