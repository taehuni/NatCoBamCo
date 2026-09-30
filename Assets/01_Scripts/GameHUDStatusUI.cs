using TMPro;
using UnityEngine;
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

    [Header("낮 / 밤")]
    public SmoothCompassUI compassUI;
    public Slider dayCycleSlider;
    public Image dayCycleFill;
    public TMP_Text dayNightIconText;
    public TMP_Text dayNightButtonText;
    public GameObject dayIconObject;
    public GameObject nightIconObject;

    [Header("남은 적")]
    public Image enemyRemainingFill;
    public TMP_Text enemyRemainingText;
    [Min(1)] public int currentWave = 1;
    [Min(0)] public int totalEnemies = 30;
    [Min(0)] public int remainingEnemies = 30;

    private bool lastNightState;

    void Awake()
    {
        if (compassUI == null)
            compassUI = GetComponentInChildren<SmoothCompassUI>(true);

        lastNightState = compassUI != null && compassUI.isNight;
        RefreshAll();
    }

    void Update()
    {
        bool isNight = compassUI != null && compassUI.isNight;

        if (isNight == lastNightState) return;

        lastNightState = isNight;
        RefreshDayNight();
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
            staminaText.text = $"{currentStamina} / {maxStamina}";
    }

    void RefreshResources()
    {
        SetResourceValue(woodText, wood);
        SetResourceValue(metalText, metal);
        SetResourceValue(partsText, parts);
    }

    void SetResourceValue(TMP_Text valueText, int value)
    {
        if (valueText == null) return;

        valueText.text = value.ToString();
        valueText.gameObject.SetActive(true);

        Transform backdrop = valueText.transform.parent != null
            ? valueText.transform.parent.Find("ValueBackdrop")
            : null;

        if (backdrop != null)
            backdrop.gameObject.SetActive(false);
    }

    void RefreshDayNight()
    {
        // U자 Slider는 항상 전체 선을 표시하고,
        // 낮/밤 상태는 중앙 버튼의 아이콘과 텍스트로 구분한다.
        float cycleProgress = 1f;

        if (dayCycleSlider != null)
        {
            dayCycleSlider.minValue = 0f;
            dayCycleSlider.maxValue = 1f;
            dayCycleSlider.SetValueWithoutNotify(cycleProgress);
        }

        if (dayCycleFill != null && dayCycleFill.material != null &&
            dayCycleFill.material.HasProperty(ProgressProperty))
            dayCycleFill.material.SetFloat(ProgressProperty, cycleProgress);

        if (dayNightIconText != null)
            dayNightIconText.text = lastNightState ? "☾" : "☀";

        if (dayNightButtonText != null)
            dayNightButtonText.text = lastNightState ? "밤" : "낮";

        if (dayIconObject != null)
            dayIconObject.SetActive(!lastNightState);

        if (nightIconObject != null)
            nightIconObject.SetActive(lastNightState);
    }

    public void ToggleDayNight()
    {
        if (compassUI == null) return;

        compassUI.isNight = !compassUI.isNight;
        lastNightState = compassUI.isNight;
        RefreshDayNight();
    }

    void RefreshEnemyRemaining()
    {
        float ratio = totalEnemies > 0 ? (float)remainingEnemies / totalEnemies : 0f;

        if (enemyRemainingFill != null)
        {
            enemyRemainingFill.fillAmount = 1f;

            Material waveMaterial = enemyRemainingFill.material;
            if (waveMaterial != null && waveMaterial.HasProperty(ProgressProperty))
                waveMaterial.SetFloat(ProgressProperty, ratio);
        }

        if (enemyRemainingText != null)
            enemyRemainingText.text = $"WAVE {currentWave}  ·  {remainingEnemies} / {totalEnemies}";
    }
}
