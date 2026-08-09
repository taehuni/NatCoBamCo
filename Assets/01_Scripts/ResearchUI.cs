using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        // EŰ�� ������ UI ���� / �ݱ�
        if (Input.GetKeyDown(KeyCode.G))
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
            messageText.text = item.itemName + " ���׷��̵� �Ϸ�!";
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