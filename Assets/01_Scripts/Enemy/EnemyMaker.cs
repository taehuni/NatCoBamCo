using UnityEngine;

public class EnemyMaker : MonoBehaviour
{
    [Header("설정")]
    public GameObject enemyPrefab;   // 생성할 적군 프리팹
    public float spawnInterval = 3f; // 생성 주기 (초)
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnEnemy();
            timer = 0f;
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy Prefab이 할당되지 않았습니다!");
            return;
        }

        // 현재 스포너 위치에서 생성
        Instantiate(enemyPrefab, transform.position, transform.rotation);
        Debug.Log("적군이 생성되었습니다!");
    }
}