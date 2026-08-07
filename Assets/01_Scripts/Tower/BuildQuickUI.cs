using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildQuickUI : MonoBehaviour
{
    [Header("단축키")]
    public KeyCode openKey = KeyCode.N;

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

    [Header("건축 데이터")]
    public List<BuildItem> wallItems = new List<BuildItem>();
    public List<BuildItem> towerItems = new List<BuildItem>();
    public List<BuildItem> buildingItems = new List<BuildItem>();

    [Header("설치 시스템")]
    public GameObject buildingSystemObject;

    [Header("UI 열릴 때 끌 스크립트")]
    public MonoBehaviour[] disableWhileOpen;

    [Header("마우스 설정")]
    public bool unlockCursorWhenOpen = true;

    private bool isOpen = false;
    private BuildCategory currentCategory = BuildCategory.Wall;

    void Start()
    {
        if (buildQuickPanel != null)
            buildQuickPanel.SetActive(false);

        if (categoryPanel != null)
            categoryPanel.SetActive(true);

        if (buildCardPanel != null)
            buildCardPanel.SetActive(false);

        if (wallButton != null)
        {
            wallButton.onClick.RemoveAllListeners();
            wallButton.onClick.AddListener(() => ShowCategory(BuildCategory.Wall));
        }

        if (towerButton != null)
        {
            towerButton.onClick.RemoveAllListeners();
            towerButton.onClick.AddListener(() => ShowCategory(BuildCategory.Tower));
        }

        if (buildingButton != null)
        {
            buildingButton.onClick.RemoveAllListeners();
            buildingButton.onClick.AddListener(() => ShowCategory(BuildCategory.Building));
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseUI);
        }
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

        if (Input.GetKeyDown(KeyCode.Alpha1))
            ShowCategory(BuildCategory.Wall);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            ShowCategory(BuildCategory.Tower);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            ShowCategory(BuildCategory.Building);

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseUI();
    }

    public void OpenUI()
    {
        if (buildQuickPanel == null) return;

        buildQuickPanel.SetActive(true);
        isOpen = true;

        // N을 눌렀을 때는 카테고리 선택창만 보이게 함
        if (categoryPanel != null)
            categoryPanel.SetActive(true);

        if (buildCardPanel != null)
            buildCardPanel.SetActive(false);

        if (unlockCursorWhenOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        SetPlayerControl(false);
    }

    public void CloseUI()
    {
        if (buildQuickPanel == null) return;

        buildQuickPanel.SetActive(false);
        isOpen = false;

        if (unlockCursorWhenOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        SetPlayerControl(true);
    }

    public void ShowCategory(BuildCategory category)
    {
        currentCategory = category;

        // 1, 2, 3 선택하면 카테고리 창은 꺼짐
        if (categoryPanel != null)
            categoryPanel.SetActive(false);

        // 카드 패널은 켜짐
        if (buildCardPanel != null)
            buildCardPanel.SetActive(true);

        ClearCards();

        if (titleText != null)
        {
            if (category == BuildCategory.Wall)
                titleText.text = "벽 종류";
            else if (category == BuildCategory.Tower)
                titleText.text = "타워 종류";
            else
                titleText.text = "건축물 종류";
        }

        List<BuildItem> list = GetCurrentList();

        CreateSideSpacer("LeftSpacer", 25f);

        for (int i = 0; i < list.Count; i++)
        {
            if (cardPrefab == null || cardContent == null) return;

            BuildCardUI card = Instantiate(cardPrefab, cardContent);
            card.Setup(list[i], this);
        }

        CreateSideSpacer("RightSpacer", 25f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(cardContent as RectTransform);

        UpdateButtonVisual();
    }

    List<BuildItem> GetCurrentList()
    {
        if (currentCategory == BuildCategory.Wall)
            return wallItems;

        if (currentCategory == BuildCategory.Tower)
            return towerItems;

        return buildingItems;
    }

    void ClearCards()
    {
        if (cardContent == null) return;

        for (int i = cardContent.childCount - 1; i >= 0; i--)
        {
            Destroy(cardContent.GetChild(i).gameObject);
        }
    }

    void CreateSideSpacer(string objectName, float width)
    {
        if (cardContent == null) return;

        GameObject spacer = new GameObject(objectName);
        spacer.transform.SetParent(cardContent, false);

        RectTransform rect = spacer.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 350f);

        LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 350f;
        layoutElement.minWidth = width;
        layoutElement.minHeight = 350f;
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

        Debug.Log("설치 선택: " + item.itemName);

        CloseUI();

        if (buildingSystemObject != null && item.buildPrefab != null)
        {
            buildingSystemObject.SendMessage(
                "StartPlacement",
                item.buildPrefab,
                SendMessageOptions.DontRequireReceiver
            );
        }
    }

    void SetPlayerControl(bool value)
    {
        if (disableWhileOpen == null) return;

        for (int i = 0; i < disableWhileOpen.Length; i++)
        {
            if (disableWhileOpen[i] != null)
            {
                disableWhileOpen[i].enabled = value;
            }
        }
    }
}