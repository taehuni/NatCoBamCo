using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private float timer;

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
        DefenseEnd
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
            Destroy(gameObject);
        }
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
        timer += Time.deltaTime;

        CheckPhaseTimer();
    }


    // 현재 페이즈의 시간이 끝났는지 확인
    private void CheckPhaseTimer()
    {
        switch (currentPhase)
        {
            case GamePhase.Gathering:

                if (Input.GetKeyDown(KeyCode.Q)) //이후 조건 변화. 현재는 디버그용 스킵.
                {
                    ChangePhase(GamePhase.NightStart);
                }

                break;


            case GamePhase.Defense:

                if (timer >= defenseTime)
                {
                    ChangePhase(GamePhase.DefenseEnd);
                }

                break;
        }
    }


    public void ChangePhase(GamePhase newPhase)
    {
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




    // 채집을 스킵하고 바로 밤으로
    public void SkipGathering()
    {
        if (currentPhase != GamePhase.Gathering)
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