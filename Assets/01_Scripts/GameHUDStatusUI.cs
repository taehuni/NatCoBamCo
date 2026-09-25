using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameHUDStatusUI : MonoBehaviour
{
    static readonly int ProgressProperty = Shader.PropertyToID("_Progress");

    [Header("스태미나")]
    public Slider staminaSlider;
    public Image staminaRingFill;
    public TMP_Text staminaText;
    [Min(1)] public int maxStamina = 100;
    [Min(0)] public int currentStamina = 100;

    [Header("자원")]
    public TMP_Text woodText;
    public TMP_Text metalText;
    public TMP_Text partsText;
    [Min(0)] public int wood;
    [Min(0)] public int metal;
    [Min(0)] public int parts;

    [Header("게임 데이터 자동 연결 (Main / Collection)")]
    public bool bindGameData;
    public TMP_Text foodText;
    public Slider healthSlider;
    public TMP_Text healthText;
    public TMP_Text fireModeText;
    public TMP_Text controlHintText;
    [Header("패배 화면")]
    public GameObject defeatPanel;
    public TMP_Text defeatMessageText;
    public Button restartButton;
    [Min(0)] public int food;

    [Header("낮 / 밤")]
    public SmoothCompassUI compassUI;
    public TMP_Text dayNightButtonText;

    [Header("남은 적")]
    public Image enemyRemainingFill;
    public TMP_Text enemyRemainingText;
    [Min(1)] public int currentWave = 1;
    [Min(0)] public int totalEnemies = 30;
    [Min(0)] public int remainingEnemies = 30;

    private bool lastNightState;
    private ResourceInventory inventory;
    private PlayerController player;
    private PlayerShoot shooter;
    private EnemyMaker[] makers = new EnemyMaker[0];
    private Material runtimeWaveMaterial;
    private float nextStatusRefresh;
    private int waveCount;
    private BuildingSystem building;
    private BuildQuickUI buildMenu;
    private ResearchUI researchMenu;

    void Awake()
    {
        if (bindGameData && enemyRemainingFill != null && enemyRemainingFill.material != null)
        {
            runtimeWaveMaterial = new Material(enemyRemainingFill.material);
            enemyRemainingFill.material = runtimeWaveMaterial;
        }
        if (compassUI == null)
            compassUI = GetComponentInChildren<SmoothCompassUI>(true);

        lastNightState = compassUI != null && compassUI.isNight;
        RefreshAll();
    }

    void Update()
    {
        if (bindGameData)
        {
            RefreshDefeat();
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
            RefreshControlHints();
            if (inventory != ResourceInventory.Instance) BindInventory();
            // Health and phase have no events. Poll at 5 Hz, including paused menus.
            if (Time.unscaledTime >= nextStatusRefresh)
            {
                nextStatusRefresh = Time.unscaledTime + 0.2f;
                RefreshGameStatus();
            }
            return;
        }

        bool isNight = compassUI != null && compassUI.isNight;

        if (isNight == lastNightState) return;

        lastNightState = isNight;
        RefreshDayNight();
    }

    void OnEnable()
    {
        if (!bindGameData) return;
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
        RefreshDefeat();
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindScene();
    }

    void Start()
    {
        if (bindGameData) BindScene();
    }

    void OnDisable()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartGame);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (inventory != null) inventory.Changed -= ReadResources;
        inventory = null;
    }

    void OnDestroy()
    {
        if (runtimeWaveMaterial != null) Destroy(runtimeWaveMaterial);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene();

    void BindScene()
    {
        player = GetComponentInParent<PlayerController>();
        shooter = player != null ? player.GetComponent<PlayerShoot>() : null;
        building = player != null ? player.GetComponent<BuildingSystem>() : null;
        buildMenu = player != null ? player.GetComponentInChildren<BuildQuickUI>(true) : null;
        researchMenu = player != null ? player.GetComponentInChildren<ResearchUI>(true) : null;
        makers = FindObjectsByType<EnemyMaker>(FindObjectsSortMode.None);
        BindInventory();
        nextStatusRefresh = 0f;
    }

    void BindInventory()
    {
        if (inventory != null) inventory.Changed -= ReadResources;
        inventory = ResourceInventory.Instance;
        if (inventory != null) inventory.Changed += ReadResources;
        ReadResources();
    }

    void RefreshControlHints()
    {
        if (researchMenu != null && researchMenu.IsOpen)
            SetLabel(controlHintText, "[1/2] 연구 탭\n[좌클릭] 연구 · [Esc] 닫기");
        else if (buildMenu != null && buildMenu.IsOpen)
            SetLabel(controlHintText, "[1-3] 항목 선택\n[F/Esc] 닫기 · [G] 철거");
        else if (building != null && building.isBuildMode)
            SetLabel(controlHintText, "[좌클릭/Enter] 설치 · [R] 회전\n[F] 건설 목록 · [G] 철거\n[Esc] 취소");
        else if (building != null && building.isRemoveMode)
            SetLabel(controlHintText, "[좌클릭] 철거 · [F] 건설\n[G/Esc] 취소");
        else
        {
            bool canReady = GameManager.Instance != null && GameManager.Instance.CanReadyForDefense;
            SetLabel(controlHintText, "[F] 건설 · [G] 철거\n[E] 상호작용 · [V] 사격 모드" +
                (canReady ? "\n[Q] 준비 완료" : ""));
        }
    }

    void RefreshDefeat()
    {
        var manager = GameManager.Instance;
        bool defeated = manager != null && manager.IsGameOver;
        if (defeatPanel != null && defeatPanel.activeSelf != defeated) defeatPanel.SetActive(defeated);
        if (!defeated) return;
        SetLabel(controlHintText, "");
        SetLabel(defeatMessageText, !string.IsNullOrEmpty(manager.RestartError) ? manager.RestartError :
            manager.IsRestarting ? "다시 시작하는 중…" : "쉘터가 파괴되었습니다");
        if (restartButton != null) restartButton.interactable = !manager.IsRestarting;
    }

    public void RestartGame()
    {
        if (bindGameData && GameManager.Instance != null) GameManager.Instance.RestartGame();
    }

    void ReadResources()
    {
        wood = inventory != null ? inventory.Get(ResourceType.Wood) : 0;
        metal = inventory != null ? inventory.Get(ResourceType.Metal) : 0;
        parts = inventory != null ? inventory.Get(ResourceType.RareMetal) : 0;
        food = inventory != null ? inventory.Get(ResourceType.Food) : 0;
        RefreshResources();
    }

    void RefreshGameStatus()
    {
        if (player != null)
        {
            if (healthSlider != null)
            {
                healthSlider.minValue = 0;
                healthSlider.maxValue = Mathf.Max(1, player.maxHp);
                healthSlider.SetValueWithoutNotify(Mathf.Clamp(player.curHp, 0, Mathf.Max(1, player.maxHp)));
            }
            SetLabel(healthText, player.curHp + " / " + player.maxHp);
        }
        var weapon = shooter != null ? shooter.currentWeapon : null;
        SetLabel(fireModeText, weapon == null ? "-" : weapon.curFireMode == Weapon.FireMode.Single
            ? "단발" : weapon.curFireMode == Weapon.FireMode.Auto ? "연발" : "점사");

        var manager = GameManager.Instance;
        lastNightState = manager != null && (manager.currentPhase == GameManager.GamePhase.NightStart ||
                                             manager.currentPhase == GameManager.GamePhase.Defense);
        RefreshDayNight();
        currentWave = waveCount = totalEnemies = remainingEnemies = 0;
        if (lastNightState)
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var maker in makers)
            {
                if (maker == null || !maker.isActiveAndEnabled || maker.gameObject.scene != activeScene) continue;
                currentWave = Mathf.Max(currentWave, maker.GetCurrentWave());
                waveCount = Mathf.Max(waveCount, maker.totalWaves);
                totalEnemies += Mathf.Max(0, maker.enemiesPerWave);
            }
            if (waveCount > 0)
                foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                    if (enemy.gameObject.scene == activeScene) remainingEnemies++;
        }
        RefreshEnemyRemaining();
    }

    static void SetLabel(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    public void SetStamina(int current, int maximum)
    {
        maxStamina = Mathf.Max(1, maximum);
        currentStamina = Mathf.Clamp(current, 0, maxStamina);
        RefreshStamina();
    }

    public void SetResources(int woodAmount, int metalAmount, int partsAmount)
    {
        wood = Mathf.Max(0, woodAmount);
        metal = Mathf.Max(0, metalAmount);
        parts = Mathf.Max(0, partsAmount);
        RefreshResources();
    }

    public void SetEnemyRemaining(int remaining, int total)
    {
        totalEnemies = Mathf.Max(0, total);
        remainingEnemies = Mathf.Clamp(remaining, 0, totalEnemies);
        RefreshEnemyRemaining();
    }

    public void SetWaveProgress(int waveNumber, int remaining, int total)
    {
        currentWave = Mathf.Max(1, waveNumber);
        SetEnemyRemaining(remaining, total);
    }

    public void RefreshAll()
    {
        RefreshStamina();
        RefreshResources();
        RefreshDayNight();
        RefreshEnemyRemaining();
    }

    void RefreshStamina()
    {
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        float ratio = maxStamina > 0 ? (float)currentStamina / maxStamina : 0f;

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        if (staminaRingFill != null)
            staminaRingFill.fillAmount = ratio;

        if (staminaText != null)
            staminaText.text = $"스태미나  {currentStamina} / {maxStamina}";
    }

    void RefreshResources()
    {
        if (bindGameData)
        {
            SetLabel(woodText, $"목재  {wood}");
            SetLabel(metalText, $"금속  {metal}");
            SetLabel(partsText, $"희귀 금속  {parts}");
            SetLabel(foodText, $"식량  {food}");
            return;
        }
        if (woodText != null) woodText.text = $"나무  {wood}";
        if (metalText != null) metalText.text = $"철  {metal}";
        if (partsText != null) partsText.text = $"고급재  {parts}";
    }

    void RefreshDayNight()
    {
        if (dayNightButtonText == null) return;

        if (bindGameData)
        {
            var manager = GameManager.Instance;
            SetLabel(dayNightButtonText, manager == null ? "-" :
                "Day " + manager.currentDay + "\n" + (lastNightState ? "밤" : "낮"));
            return;
        }

        dayNightButtonText.text = lastNightState ? "달" : "해";
    }

    void RefreshEnemyRemaining()
    {
        float ratio = totalEnemies > 0 ? (float)remainingEnemies / totalEnemies : 0f;

        if (enemyRemainingFill != null)
        {
            enemyRemainingFill.fillAmount = 1f;

            Material waveMaterial = enemyRemainingFill.material;
            if (waveMaterial != null && waveMaterial.HasProperty(ProgressProperty))
                waveMaterial.SetFloat(ProgressProperty, Mathf.Clamp01(ratio));
        }

        if (bindGameData)
        {
            // Count only enemies already spawned; future spawns are not included.
            SetLabel(enemyRemainingText, GameManager.Instance == null ? "-" : !lastNightState
                ? "수집 / 건설" : waveCount == 0 ? "밤 / 기지 방어"
                : $"WAVE {currentWave}/{waveCount} · 현재 적 {remainingEnemies}");
            return;
        }

        if (enemyRemainingText != null)
            enemyRemainingText.text = $"WAVE {currentWave}  ·  {remainingEnemies} / {totalEnemies}";
    }
}
