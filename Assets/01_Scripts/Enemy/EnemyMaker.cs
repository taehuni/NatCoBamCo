using UnityEngine;

public class EnemyMaker : MonoBehaviour
{
    [Header("Enemy")]
    public GameObject enemyPrefab; //적 프리팹

    [Header("Spawn")]
    public float spawnInterval = 1f;

    [Header("Wave")]
    public int totalWaves = 5; //총합 적 웨이브
    public int enemiesPerWave = 5; //한 웨이브당 나오는 적의 수
    public float waveDelay = 2f; //웨이브 딜레이

    private float spawnTimer;
    private float waveTimer;

    private int currentWave = 0;
    private int spawnedThisWave = 0;

    private bool spawningWave = false;
    private bool waitingNextWave = false;


    void Update()
    {
        if (GameManager.Instance == null)
            return;

        // Defense가 아니면 아무것도 하지 않음
        if (GameManager.Instance.currentPhase !=
            GameManager.GamePhase.Defense)
        {
            return;
        }


        // 다음 웨이브 대기 중
        if (waitingNextWave)
        {
            waveTimer += Time.deltaTime;

            if (waveTimer >= waveDelay)
            {
                waitingNextWave = false;
                StartNextWave();
            }

            return;
        }


        // 현재 웨이브를 아직 시작하지 않았다면
        if (!spawningWave &&
            currentWave < totalWaves)
        {
            StartNextWave();
        }


        // 현재 웨이브 적 생성
        if (spawningWave)
        {
            SpawnWaveEnemies();
        }


        // 현재 웨이브의 모든 적이 죽었는지 확인
        CheckWaveClear();
    }

    /**적 생성*/
    void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy Prefab이 존재하지 않습니다!");
            return;
        }

        // 적 생성
        Instantiate(enemyPrefab, transform.position, transform.rotation);
        Debug.Log(
            $"적 생성됨! ({spawnedThisWave + 1}/{enemiesPerWave})"
        );
    }

    /** 웨이브 시작 함수*/
    void StartNextWave()
    {
        currentWave++;

        spawnedThisWave = 0;
        spawnTimer = 0f;

        spawningWave = true;

        Debug.Log(
            $"===== Wave {currentWave} 시작 ====="
        );
    }

    //**현재 웨이브 적 생성*/
    void SpawnWaveEnemies()
    {
        spawnTimer += Time.deltaTime;

        if (spawnedThisWave >= enemiesPerWave)
        {
            spawningWave = false;
            return;
        }


        if (spawnTimer >= spawnInterval)
        {
            SpawnEnemy();

            spawnedThisWave++;
            spawnTimer = 0f;
        }
    }

    /**웨이브 클리어 확인*/
    void CheckWaveClear()
    {
        // 아직 적 생성 중이면 검사하지 않음
        if (spawningWave)
            return;


        // 현재 씬의 적 확인
        EnemyAI[] enemies =
            FindObjectsOfType<EnemyAI>();


        // 아직 적이 살아있음
        if (enemies.Length > 0)
            return;


        // 마지막 웨이브까지 끝났으면
        if (currentWave >= totalWaves)
        {
            Debug.Log("모든 웨이브 방어 성공!");

            GameManager.Instance.CompleteDefense();

            return;
        }


        // 다음 웨이브 대기
        waitingNextWave = true;
        waveTimer = 0f;

        Debug.Log(
            $"Wave {currentWave} 클리어! " +
            $"{waveDelay}초 후 다음 웨이브"
        );
    }

    /**디버그용*/
    public int GetCurrentWave()
    {
        return currentWave;
    }
}