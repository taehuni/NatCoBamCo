using UnityEngine;

public class EnemyMaker : MonoBehaviour
{
    [Header("Enemy")]
    public GameObject enemyPrefab;   // 적 프리팹
    public float spawnInterval = 3f; // 
    private float timer;

    void Update()
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Defense)
        {
            return;
        }

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
            Debug.LogError("Enemy Prefab이 존재하지 않습니다!");
            return;
        }

        // 적 생성
        Instantiate(enemyPrefab, transform.position, transform.rotation);
        Debug.Log("적 생성됨!");
    }
}