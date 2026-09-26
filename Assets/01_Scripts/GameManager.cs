using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private float timer;
    private float? savedCoreHealth;
    [SerializeField] private string restartSceneName = "01_Main";
    public bool IsGameOver => currentPhase == GamePhase.Defeated;
    public bool IsRestarting { get; private set; }
    public string RestartError { get; private set; }
    public bool CanReadyForDefense => !IsRestarting &&
        currentPhase == GamePhase.Gathering &&
        SceneManager.GetActiveScene().name == "01_Main";

    [Header("게임 시간")]
    public float gatheringTime = 60f; //테스트용 채집 시간 이후 제거
    public float defenseTime = 10f; //디펜스 시간. 이후 변경 가능

    [Header("현재 날짜")]
    public int currentDay = 1;

    [Header("빛")]
    public Light mainLight;

    public Color dayLightColor = new Color32(255, 244, 214, 255); // FFF4D6
    public Color nightLightColor = new Color32(0, 0, 0, 255);     // 000000

    public enum GamePhase
    {
        DayStart,
        Gathering,
        NightStart,
        Defense,
        DefenseEnd,
        Defeated
    }

    public GamePhase currentPhase;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }


    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        
        ChangePhase(GamePhase.DayStart);
    }

    private void OnSceneLoaded(
       Scene scene,
       LoadSceneMode mode)
    {
        Debug.Log(
            "씬 로드 완료 : " + scene.name
        );

        FindMainLight();

        ApplyCurrentLight();
    }

    internal void RememberCoreHealth(Core core)
    {
        // Restart unloads the old core after its saved state has been cleared.
        if (IsRestarting || core == null || core.gameObject.scene.name != "01_Main") return;
        savedCoreHealth = Mathf.Clamp(core.curHp, 0f, core.maxHp);
    }

    internal void RestoreCoreHealth(Core core)
    {
        if (IsRestarting || !savedCoreHealth.HasValue || core == null ||
            core.gameObject.scene.name != "01_Main") return;
        core.curHp = Mathf.Clamp(savedCoreHealth.Value, 0f, core.maxHp);
    }

    private void FindMainLight()
    {
        GameObject lightObject =
            GameObject.FindGameObjectWithTag("MainLight");

        if (lightObject == null)
        {
            mainLight = null;

            Debug.LogWarning(
                "MainLight 태그를 가진 오브젝트가 없습니다."
            );

            return;
        }

        mainLight =
            lightObject.GetComponent<Light>();
    }

    private void ApplyCurrentLight()
    {
        if (mainLight == null)
            return;


        switch (currentPhase)
        {
            case GamePhase.DayStart:
            case GamePhase.Gathering:
            case GamePhase.DefenseEnd:

                SetDayLight();

                break;


            case GamePhase.NightStart:
            case GamePhase.Defense:

                SetNightLight();

                break;
        }
    }


    private void Update()
    {
        if (IsGameOver || IsRestarting) return;
        timer += Time.deltaTime;

        CheckPhaseTimer();
    }


    // 현재 페이즈의 시간이 끝났는지 확인
    private void CheckPhaseTimer()
    {
        switch (currentPhase)
        {
            case GamePhase.Gathering:

                // Q: Main에서 준비 완료. 메뉴나 건설/철거 조작 중에는 밤을 시작하지 않는다.
                if (Input.GetKeyDown(KeyCode.Q) &&
                    InteractionSelection.CanUseWorldActions(FindFirstObjectByType<PlayerController>()))
                {
                    SkipGathering();
                }

                break;


            case GamePhase.Defense:

                //이후 디펜스 페이즈 시간 계산에 사용. (n초 이상이면 강제 패배 등.)

                break;
        }
    }

    public void CompleteDefense()
    {
        if (currentPhase != GamePhase.Defense)
        {
            return;
        }

        Debug.Log("모든 웨이브 방어 성공!");

        ChangePhase(GamePhase.DefenseEnd);
    }

    public void ChangePhase(GamePhase newPhase)
    {
        if (IsGameOver || IsRestarting) return;
        if (newPhase == GamePhase.Defeated)
        {
            EndGame();
            return;
        }
        currentPhase = newPhase;

        // 페이즈가 바뀌면 타이머 초기화
        timer = 0f;

        Debug.Log("현재 페이즈 : " + currentPhase);

        switch (currentPhase)
        {
            case GamePhase.DayStart:
                StartDay();
                break;

            case GamePhase.Gathering:
                StartGathering();
                break;

            case GamePhase.NightStart:
                StartNight();
                break;

            case GamePhase.Defense:
                StartDefense();
                break;

            case GamePhase.DefenseEnd:
                EndDefense();
                break;
        }
    }


    void StartDay()
    {
        Debug.Log("낮 시작");

        SetDayLight();
        ClearEnemies();

        // 다음 단계
        ChangePhase(GamePhase.Gathering);
    }


    void StartGathering()
    {
        Debug.Log("채집 시작");
    }


    void StartNight()
    {
        Debug.Log("밤 시작");

        SetNightLight();

        // 다음 단계
        ChangePhase(GamePhase.Defense);
    }


    void StartDefense()
    {
        Debug.Log("디펜스 시작");
    }


    void EndDefense()
    {
        Debug.Log("디펜스 종료");

        currentDay++;

        // 다음 날
        ChangePhase(GamePhase.DayStart);
    }




    // Main에서 준비를 마치고 밤으로
    public void SkipGathering()
    {
        if (!CanReadyForDefense)
            return;

        ChangePhase(GamePhase.NightStart);
    }


    // 디펜스 강제 종료 (디버그용)
    public void SkipDefense()
    {
        if (currentPhase != GamePhase.Defense)
            return;

        ChangePhase(GamePhase.DefenseEnd);
    }


    // 현재 페이즈 진행 시간
    public float GetTimer()
    {
        return timer;
    }


    void SetDayLight()
    {
        if (mainLight == null)
            return;

        mainLight.color = dayLightColor;
    }

    void SetNightLight()
    {
        if (mainLight == null)
            return;

        mainLight.color = nightLightColor;
    }

    public void EndGame()
    {
        if (IsGameOver || IsRestarting) return;
        currentPhase = GamePhase.Defeated;
        timer = 0f;
        RestartError = null;

        // Close menus before freezing: ResearchUI restores its previous time scale.
        foreach (var menu in FindObjectsByType<BuildQuickUI>(FindObjectsSortMode.None)) menu.CloseUI();
        foreach (var menu in FindObjectsByType<ResearchUI>(FindObjectsSortMode.None)) menu.CloseUI();
        foreach (var building in FindObjectsByType<BuildingSystem>(FindObjectsSortMode.None))
        {
            building.CancelPlacement();
            building.enabled = false;
        }
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) player.enabled = false;
        foreach (var camera in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None)) camera.enabled = false;
        foreach (var shooter in FindObjectsByType<PlayerShoot>(FindObjectsSortMode.None))
        {
            shooter.StopAllCoroutines();
            shooter.enabled = false;
        }
        foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            enemy.StopAllCoroutines();
            enemy.enabled = false;
        }
        foreach (var building in FindObjectsByType<BuildingObject>(FindObjectsSortMode.None))
            foreach (var behaviour in building.GetComponentsInChildren<MonoBehaviour>())
            {
                behaviour.StopAllCoroutines();
                behaviour.enabled = false;
            }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        if (!IsGameOver || IsRestarting) return;
        if (string.IsNullOrWhiteSpace(restartSceneName) || !Application.CanStreamedLevelBeLoaded(restartSceneName))
        {
            RestartError = "다시 시작할 수 없습니다.";
            Debug.LogWarning("Restart scene is missing from the build scene list.", this);
            return;
        }
        IsRestarting = true;
        RestartError = null;
        StartCoroutine(RestartSession());
    }

    IEnumerator RestartSession()
    {
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(restartSceneName, LoadSceneMode.Single);
            if (operation != null) operation.allowSceneActivation = false;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
        if (operation == null)
        {
            IsRestarting = false;
            RestartError = "다시 시작할 수 없습니다.";
            yield break;
        }

        // Keep the previous session until the new scene is ready to activate.
        while (operation.progress < 0.9f) yield return null;
        foreach (var player in FindObjectsByType<PlayerScenePersistence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroySessionObject(player.gameObject);
        foreach (var inventory in FindObjectsByType<ResourceInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroySessionObject(inventory.gameObject);
        foreach (var buildings in FindObjectsByType<BuiltBuildingPersistence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroySessionObject(buildings.gameObject);
        foreach (var survivors in FindObjectsByType<SurvivorManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroySessionObject(survivors.gameObject);
        // Destroy must finish before new singleton instances execute Awake.
        yield return null;
        ResourceNode.ResetSession();
        DailyResourceSpawner.ResetSession();
        SurvivorRescueEvent.ResetSession();
        InteractionSelection.ResetSession();
        savedCoreHealth = null;
        currentDay = 1;
        timer = 0f;
        operation.allowSceneActivation = true;
        yield return operation;

        currentPhase = GamePhase.DayStart;
        IsRestarting = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        FindMainLight();
        ChangePhase(GamePhase.DayStart);
    }

    void DestroySessionObject(GameObject target)
    {
        if (target == null || target == gameObject) return;
        target.SetActive(false);
        Destroy(target);
    }

    void ClearEnemies()
    {
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();

        foreach (EnemyAI enemy in enemies)
        {
            Destroy(enemy.gameObject);
        }

        Debug.Log("남아있는 적 제거 : " + enemies.Length);
    }

}
