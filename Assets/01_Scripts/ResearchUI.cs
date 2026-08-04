using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResearchUI : MonoBehaviour
{
    [Header("전체 UI")]
    public GameObject researchPanel;

    [Header("탭 버튼")]
    public Button wallTabButton;
    public Button towerTabButton;
    public Button closeButton;

    [Header("카드 생성")]
    public ResearchCardUI cardPrefab;
    public Transform cardContent;

    [Header("연구 데이터")]
    public List<ResearchItem> wallResearchItems = new List<ResearchItem>();
    public List<ResearchItem> towerResearchItems = new List<ResearchItem>();

    [Header("안내 텍스트")]
    public TMP_Text messageText;

    [Header("UI 열릴 때 끌 스크립트")]
    public MonoBehaviour[] disableWhileOpen;

    [Header("게임 일시정지")]
    public bool pauseGameWhileOpen = true;

    private ResearchCategory currentCategory = ResearchCategory.Wall;
    private bool isOpen = false;

    void Start()
    {
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
        // E키로 연구소 UI 열기 / 닫기
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isOpen)
            {
                CloseUI();
            }
            else
            {
                OpenUI();
            }
        }

        if (!isOpen) return;

        // 1번: 벽 탭
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ShowCategory(ResearchCategory.Wall);
        }

        // 2번: 타워 탭
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ShowCategory(ResearchCategory.Tower);
        }

        // ESC: 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUI();
        }
    }

    public void OpenUI()
    {
        if (researchPanel == null) return;

        researchPanel.SetActive(true);
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseGameWhileOpen)
        {
            Time.timeScale = 0f;
        }

        SetPlayerControl(false);
        ShowCategory(currentCategory);
    }

    public void CloseUI()
    {
        if (researchPanel == null) return;

        researchPanel.SetActive(false);
        isOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseGameWhileOpen)
        {
            Time.timeScale = 1f;
        }

        SetPlayerControl(true);
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
        if (item == null) return;

        item.level++;

        if (messageText != null)
        {
            messageText.text = item.itemName + " 업그레이드 완료!";
        }

        ShowCategory(currentCategory);
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