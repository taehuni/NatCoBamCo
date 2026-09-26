using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-90)]
public class ResearchUI : MonoBehaviour
{
    [Header("��ü UI")]
    public GameObject researchPanel;

    [Header("�� ��ư")]
    public Button wallTabButton;
    public Button towerTabButton;
    public Button closeButton;

    [Header("ī�� ����")]
    public ResearchCardUI cardPrefab;
    public Transform cardContent;

    [Header("���� ������")]
    public List<ResearchItem> wallResearchItems = new List<ResearchItem>();
    public List<ResearchItem> towerResearchItems = new List<ResearchItem>();

    [Header("�ȳ� �ؽ�Ʈ")]
    public TMP_Text messageText;

    [Header("UI ���� �� �� ��ũ��Ʈ")]
    public MonoBehaviour[] disableWhileOpen;

    [Header("���� �Ͻ�����")]
    public bool pauseGameWhileOpen = false;

    private ResearchCategory currentCategory = ResearchCategory.Wall;
    private bool isOpen = false;
    public bool IsOpen => isOpen;
    private float timeScaleBeforeOpen = 1f;
    private PlayerController playerController;
    private CameraFollow cameraFollow;
    private bool playerControllerWasEnabled;
    private bool playerControllerStateCaptured;
    private bool cameraFollowWasEnabled;
    private bool cameraFollowStateCaptured;
    private bool upgrading;
    private int lastUpgradeFrame = -1;
    public ResearchItem ActiveResearch { get; private set; }
    public float RemainingResearchTime { get; private set; }
    public float ActiveResearchDuration { get; private set; }

    bool CanProgress => GameManager.Instance == null ||
        (!GameManager.Instance.IsGameOver && !GameManager.Instance.IsRestarting);

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyExistingBuildings();

    void Start()
    {
        CachePlayerControls();
        ApplyExistingBuildings();

        if (researchPanel != null)
        {
            researchPanel.SetActive(false);
        }

        if (wallTabButton != null)
        {
            wallTabButton.onClick.RemoveAllListeners();
            wallTabButton.onClick.AddListener(() => ShowCategory(ResearchCategory.Wall));
        }

        if (towerTabButton != null)
        {
            towerTabButton.onClick.RemoveAllListeners();
            towerTabButton.onClick.AddListener(() => ShowCategory(ResearchCategory.Tower));
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseUI);
        }

        ShowCategory(ResearchCategory.Wall);
    }

    void Update()
    {
        TickResearch();
        if (!isOpen) return;

        // 1��: �� ��
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ShowCategory(ResearchCategory.Wall);
        }

        // 2��: Ÿ�� ��
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ShowCategory(ResearchCategory.Tower);
        }

        // ESC: �ݱ�
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUI();
        }
    }

    public void OpenUI()
    {
        if (researchPanel == null || isOpen || !InteractionSelection.TryOpenMenu(this)) return;

        researchPanel.SetActive(true);
        researchPanel.transform.SetAsLastSibling();
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseGameWhileOpen)
        {
            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;
        }

        SetPlayerControl(false);
        ShowCategory(currentCategory);
    }

    public void CloseUI()
    {
        if (!isOpen) return;

        if (researchPanel != null) researchPanel.SetActive(false);
        isOpen = false;
        InteractionSelection.CloseMenu(this);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseGameWhileOpen)
        {
            Time.timeScale = timeScaleBeforeOpen;
        }

        SetPlayerControl(true);
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (isOpen) CloseUI();
    }

    public void ShowCategory(ResearchCategory category)
    {
        currentCategory = category;

        ClearCards();

        List<ResearchItem> list = category == ResearchCategory.Wall
            ? wallResearchItems
            : towerResearchItems;

        for (int i = 0; i < list.Count; i++)
        {
            if (cardPrefab == null || cardContent == null) return;

            ResearchCardUI card = Instantiate(cardPrefab, cardContent);
            card.Setup(list[i], this);
        }

        UpdateTabVisual();
    }

    void ClearCards()
    {
        if (cardContent == null) return;

        for (int i = cardContent.childCount - 1; i >= 0; i--)
        {
            cardContent.GetChild(i).gameObject.SetActive(false);
            Destroy(cardContent.GetChild(i).gameObject);
        }
    }

    void UpdateTabVisual()
    {
        Color selectedColor = new Color(0.2f, 0.8f, 0.6f, 1f);
        Color normalColor = Color.white;

        Image wallImage = null;
        Image towerImage = null;

        if (wallTabButton != null)
        {
            wallImage = wallTabButton.GetComponent<Image>();
        }

        if (towerTabButton != null)
        {
            towerImage = towerTabButton.GetComponent<Image>();
        }

        if (wallImage != null)
        {
            wallImage.color = currentCategory == ResearchCategory.Wall
                ? selectedColor
                : normalColor;
        }

        if (towerImage != null)
        {
            towerImage.color = currentCategory == ResearchCategory.Tower
                ? selectedColor
                : normalColor;
        }
    }

    public void UpgradeItem(ResearchItem item)
    {
        if (upgrading || lastUpgradeFrame == Time.frameCount || !CanUpgrade(item)) return;
        upgrading = true;
        try
        {
            // Snapshot the duration before payment callbacks or later roster changes.
            float duration = GetResearchDuration(item);
            if (!ResourceInventory.Instance.TrySpend(item.cost.ToResources())) return;
            ActiveResearch = item;
            ActiveResearchDuration = duration;
            RemainingResearchTime = duration;
            lastUpgradeFrame = Time.frameCount;
            if (messageText != null) messageText.text = item.itemName + " 연구 시작";
        }
        finally
        {
            upgrading = false;
        }
        ShowCategory(currentCategory);
    }

    public bool CanUpgrade(ResearchItem item) => isActiveAndEnabled && isOpen && !upgrading &&
        ActiveResearch == null && CanProgress &&
        item != null && (towerResearchItems.Contains(item) || wallResearchItems.Contains(item)) && item.IsConfigured &&
        item.researchDuration > 0f && !float.IsInfinity(item.researchDuration) &&
        item.level >= 0 && item.level < item.MaxResearchLevel && ResourceInventory.Instance != null &&
        ResourceInventory.Instance.CanAfford(item.cost.ToResources());

    public float GetResearchDuration(ResearchItem item) => item == null ? 0f :
        item.researchDuration * (SurvivorManager.Instance != null
            ? SurvivorManager.Instance.GetResearchSpeedMultiplier() : 1f);

    public string GetResearchButtonText(ResearchItem item)
    {
        if (item == null || !item.IsConfigured) return "준비 중";
        if (item.level >= item.MaxResearchLevel) return "연구 완료";
        if (!CanProgress) return "진행 불가";
        if (ActiveResearch == item) return "연구 중";
        if (ActiveResearch != null) return "다른 연구 중";
        if (CanUpgrade(item)) return "연구하기";
        return ResourceInventory.Instance != null &&
            !ResourceInventory.Instance.CanAfford(item.cost.ToResources()) ? "재료 부족" : "진행 불가";
    }

    void TickResearch()
    {
        // This component stays with the player; closing its panel must not stop research.
        if (ActiveResearch == null || !CanProgress || Time.timeScale <= 0f) return;
        RemainingResearchTime = Mathf.Max(0f, RemainingResearchTime - Time.deltaTime);
        if (RemainingResearchTime > 0f) return;

        var completed = ActiveResearch;
        ActiveResearch = null;
        ActiveResearchDuration = 0f;
        completed.level = Mathf.Min(completed.level + 1, completed.MaxResearchLevel);
        ApplyExistingBuildings();
        if (messageText != null) messageText.text = completed.itemName + " 연구 완료!";
        if (isOpen) ShowCategory(currentCategory);
    }

    public void ApplyResearchTo(GameObject building)
    {
        foreach (var item in wallResearchItems)
            if (item != null) item.ApplyTo(building);
        foreach (var item in towerResearchItems)
            if (item != null) item.ApplyTo(building);
    }

    void ApplyExistingBuildings()
    {
        // Include retained buildings in an unloaded scene; exclude build previews.
        foreach (var building in FindObjectsByType<BuildingObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (building.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")) continue;
            if (building.GetComponentInParent<BuiltBuildingPersistence>(true) != null ||
                building.gameObject.scene.name == "01_Main" || building.gameObject.scene.name == "02_Collection")
                ApplyResearchTo(building.gameObject);
        }
    }

    void SetPlayerControl(bool value)
    {
        CachePlayerControls();

        if (!value)
        {
            if (playerController != null && !playerControllerStateCaptured)
            {
                playerControllerWasEnabled = playerController.enabled;
                playerControllerStateCaptured = true;
                playerController.enabled = false;
            }

            if (cameraFollow != null && !cameraFollowStateCaptured)
            {
                cameraFollowWasEnabled = cameraFollow.enabled;
                cameraFollowStateCaptured = true;
                cameraFollow.enabled = false;
            }
        }
        else
        {
            if (playerController != null && playerControllerStateCaptured)
            {
                playerController.enabled = playerControllerWasEnabled;
                playerControllerStateCaptured = false;
            }

            if (cameraFollow != null && cameraFollowStateCaptured)
            {
                cameraFollow.enabled = cameraFollowWasEnabled;
                cameraFollowStateCaptured = false;
            }
        }

        if (disableWhileOpen == null) return;

        for (int i = 0; i < disableWhileOpen.Length; i++)
        {
            if (disableWhileOpen[i] != null)
            {
                disableWhileOpen[i].enabled = value;
            }
        }
    }

    void CachePlayerControls()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        }

        if (cameraFollow == null)
        {
            cameraFollow = FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        }
    }
}
