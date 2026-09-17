using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildQuickUI : MonoBehaviour
{
    [Header("단축키")]
    public KeyCode openKey = KeyCode.B;

    [Header("전체 UI")]
    public GameObject buildQuickPanel;
    public GameObject categoryPanel;
    public GameObject buildCardPanel;

    [Header("카테고리 버튼")]
    public Button wallButton;
    public Button towerButton;
    public Button buildingButton;
    public Button closeButton;

    [Header("카드 생성")]
    public BuildCardUI cardPrefab;
    public Transform cardContent;

    [Header("제목")]
    public TMP_Text titleText;

    [Header("건설 데이터")]
    public List<BuildItem> wallItems = new List<BuildItem>();
    public List<BuildItem> towerItems = new List<BuildItem>();
    public List<BuildItem> buildingItems = new List<BuildItem>();

    [Header("배치 시스템")]
    public GameObject buildingSystemObject;

    [Header("카드 UI 크기")]
    public Vector2 cardPanelSize = new Vector2(900f, 560f);
    public Vector2 cardAreaSize = new Vector2(840f, 450f);
    public Vector2 cardSize = new Vector2(230f, 390f);
    [Min(0f)] public float cardSpacing = 20f;

    private bool isOpen = false;
    private BuildCategory currentCategory = BuildCategory.Wall;
    private PlayerController playerController;
    private CameraFollow cameraFollow;
    private bool playerControllerWasEnabled;
    private bool playerControllerStateCaptured;
    private bool cameraFollowWasEnabled;
    private bool cameraFollowStateCaptured;

    void Start()
    {
        ConfigureCardLayout();

        playerController = GetComponentInParent<PlayerController>(true);
        Transform playerRoot = playerController != null ? playerController.transform : transform.root;
        cameraFollow = playerRoot.GetComponentInChildren<CameraFollow>(true);
        BuildingSystem linkedBuildingSystem = playerRoot.GetComponentInChildren<BuildingSystem>(true);

        if (buildingSystemObject == null && linkedBuildingSystem != null)
            buildingSystemObject = linkedBuildingSystem.gameObject;

        if (buildQuickPanel != null)
            buildQuickPanel.SetActive(false);

        if (categoryPanel != null)
            categoryPanel.SetActive(true);

        if (buildCardPanel != null)
            buildCardPanel.SetActive(false);

        ConfigureKeyboardOnlyButton(wallButton, 1);
        ConfigureKeyboardOnlyButton(towerButton, 2);
        ConfigureKeyboardOnlyButton(buildingButton, 3);
        ConfigureKeyboardOnlyButton(closeButton, 0);
    }

    void Update()
    {
        if (Input.GetKeyDown(openKey))
        {
            if (isOpen)
                CloseUI();
            else
                OpenUI();
        }

        if (!isOpen) return;

        int pressedNumber = GetPressedNumber();

        if (categoryPanel != null && categoryPanel.activeSelf)
        {
            if (pressedNumber == 1)
                ShowCategory(BuildCategory.Wall);
            else if (pressedNumber == 2)
                ShowCategory(BuildCategory.Tower);
            else if (pressedNumber == 3)
                ShowCategory(BuildCategory.Building);
        }
        else if (buildCardPanel != null && buildCardPanel.activeSelf && pressedNumber > 0)
        {
            SelectBuildItemByNumber(pressedNumber);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseUI();
    }

    public void OpenUI()
    {
        if (buildQuickPanel == null) return;

        buildQuickPanel.SetActive(true);
        isOpen = true;

        // N을 누르면 항상 카테고리 선택창부터 보이게 함
        if (categoryPanel != null)
            categoryPanel.SetActive(true);

        if (buildCardPanel != null)
            buildCardPanel.SetActive(false);

        // 키보드 전용 UI이므로 마우스 커서를 풀지 않는다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetPlayerControl(false);
    }

    public void CloseUI()
    {
        if (buildQuickPanel == null) return;

        buildQuickPanel.SetActive(false);
        isOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetPlayerControl(true);
    }

    public void ShowCategory(BuildCategory category)
    {
        currentCategory = category;

        if (categoryPanel != null)
            categoryPanel.SetActive(false);

        if (buildCardPanel != null)
            buildCardPanel.SetActive(true);

        ClearCards();

        if (titleText != null)
        {
            if (category == BuildCategory.Wall)
                titleText.text = "벽 선택";
            else if (category == BuildCategory.Tower)
                titleText.text = "타워 선택";
            else
                titleText.text = "건물 선택";
        }

        List<BuildItem> list = GetCurrentList();
        CenterCardContent(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            if (cardPrefab == null || cardContent == null) return;

            BuildCardUI card = Instantiate(cardPrefab, cardContent);
            card.Setup(list[i], this);

            LayoutElement cardLayout = card.GetComponent<LayoutElement>();

            if (cardLayout != null)
            {
                cardLayout.minWidth = cardSize.x;
                cardLayout.preferredWidth = cardSize.x;
                cardLayout.minHeight = cardSize.y;
                cardLayout.preferredHeight = cardSize.y;
            }

            if (card.placeButtonText != null)
                card.placeButtonText.text = (i + 1).ToString();

            if (card.placeButton != null)
            {
                card.placeButton.onClick.RemoveAllListeners();
                card.placeButton.enabled = false;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardContent as RectTransform);

        UpdateButtonVisual();
    }

    void CenterCardContent(int cardCount)
    {
        if (cardContent == null) return;

        RectTransform contentRect = cardContent as RectTransform;

        if (contentRect == null) return;

        float totalWidth = cardCount > 0
            ? cardSize.x * cardCount + cardSpacing * (cardCount - 1)
            : 0f;

        ContentSizeFitter fitter = cardContent.GetComponent<ContentSizeFitter>();

        if (fitter != null)
            fitter.enabled = false;

        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(totalWidth, cardSize.y);
    }

    List<BuildItem> GetCurrentList()
    {
        if (currentCategory == BuildCategory.Wall)
            return wallItems;

        if (currentCategory == BuildCategory.Tower)
            return towerItems;

        return buildingItems;
    }

    void SelectBuildItemByNumber(int shortcutNumber)
    {
        List<BuildItem> list = GetCurrentList();
        int index = shortcutNumber - 1;

        if (index >= 0 && index < list.Count)
            SelectBuildItem(list[index]);
    }

    int GetPressedNumber()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        return 0;
    }

    void ConfigureCardLayout()
    {
        if (buildCardPanel == null) return;

        RectTransform panelRect = buildCardPanel.transform as RectTransform;

        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localScale = Vector3.one;
            panelRect.sizeDelta = cardPanelSize;
        }

        ScrollRect scrollRect = buildCardPanel.GetComponentInChildren<ScrollRect>(true);

        if (scrollRect != null)
        {
            RectTransform scrollTransform = scrollRect.transform as RectTransform;

            if (scrollTransform != null)
            {
                scrollTransform.anchorMin = new Vector2(0.5f, 0.5f);
                scrollTransform.anchorMax = new Vector2(0.5f, 0.5f);
                scrollTransform.pivot = new Vector2(0.5f, 0.5f);
                scrollTransform.anchoredPosition = new Vector2(0f, -25f);
                scrollTransform.sizeDelta = cardAreaSize;
            }

            scrollRect.horizontal = false;
            scrollRect.vertical = false;

            if (scrollRect.horizontalScrollbar != null)
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);

            if (scrollRect.viewport != null)
                scrollRect.viewport.sizeDelta = Vector2.zero;

            scrollRect.enabled = false;
        }

        HorizontalLayoutGroup layout = cardContent != null
            ? cardContent.GetComponent<HorizontalLayoutGroup>()
            : null;

        if (layout != null)
        {
            layout.spacing = cardSpacing;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        RectTransform closeRect = closeButton != null
            ? closeButton.transform as RectTransform
            : null;

        if (closeRect != null)
        {
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-15f, -15f);
        }

        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -15f);
            titleRect.sizeDelta = new Vector2(-100f, 50f);
        }
    }

    void ConfigureKeyboardOnlyButton(Button button, int shortcutNumber)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.enabled = false;

        if (shortcutNumber <= 0) return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        string prefix = shortcutNumber + ". ";

        if (label != null && label.text.StartsWith(prefix))
            label.text = label.text.Substring(prefix.Length);
    }

    void ClearCards()
    {
        if (cardContent == null) return;

        for (int i = cardContent.childCount - 1; i >= 0; i--)
        {
            Destroy(cardContent.GetChild(i).gameObject);
        }
    }

    void UpdateButtonVisual()
    {
        Color selected = new Color(0.2f, 0.8f, 0.6f, 1f);
        Color normal = Color.white;

        SetButtonColor(wallButton, currentCategory == BuildCategory.Wall, selected, normal);
        SetButtonColor(towerButton, currentCategory == BuildCategory.Tower, selected, normal);
        SetButtonColor(buildingButton, currentCategory == BuildCategory.Building, selected, normal);
    }

    void SetButtonColor(Button button, bool isSelected, Color selected, Color normal)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();

        if (image != null)
            image.color = isSelected ? selected : normal;
    }

    public void SelectBuildItem(BuildItem item)
    {
        if (item == null) return;

        Debug.Log("배치 선택: " + item.itemName);

        if (item.buildPrefab == null || !EnsureRuntimeCost(item)) return;

        BuildingSystem placement = FindFirstObjectByType<BuildingSystem>();
        if (placement == null) return;

        buildingSystemObject = placement.gameObject;
        CloseUI();
        placement.StartPlacement(item);
    }

    bool EnsureRuntimeCost(BuildItem item)
    {
        if (item.cost != null && item.cost.IsValid) return true;

        if (item.cost == null)
        {
            item.cost = new BuildCost();
        }

        string text = item.costText ?? string.Empty;
        item.cost.wood = ReadCost(text, "목재");
        item.cost.metal = ReadCost(text, "철");
        item.cost.rareMetal = ReadCost(text, "부품");
        item.cost.food = ReadCost(text, "식량");
        item.cost.configured = true;
        return item.cost.IsValid;
    }

    int ReadCost(string text, string label)
    {
        Match match = Regex.Match(text, Regex.Escape(label) + @"\s*(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : 0;
    }

    void SetPlayerControl(bool value)
    {
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

            return;
        }

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
}
