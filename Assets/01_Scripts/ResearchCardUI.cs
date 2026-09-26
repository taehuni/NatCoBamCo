using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResearchCardUI : MonoBehaviour
{
    [Header("UI")]
    public Image itemImage;
    public TMP_Text nameText;
    public TMP_Text costText;
    public TMP_Text currentStatText;
    public TMP_Text nextStatText;
    public TMP_Text specialEffectText;
    public Button upgradeButton;
    public TMP_Text upgradeButtonText;

    private ResearchItem currentItem;
    private ResearchUI researchUI;
    private float nextButtonRefresh;

    void Update()
    {
        if (Time.unscaledTime < nextButtonRefresh) return;
        nextButtonRefresh = Time.unscaledTime + 0.2f;
        RefreshButton();
    }

    public void Setup(ResearchItem item, ResearchUI ui)
    {
        currentItem = item;
        researchUI = ui;

        bool configured = item.IsConfigured;
        nameText.text = item.itemName + (configured ? $" {item.level}/{item.MaxResearchLevel}" : "");
        costText.text = configured ? item.cost.DisplayText().Replace("소모 자원: ", "").Replace("희귀 금속", "희귀금속") : "준비 중";
        item.GetStatDescriptions(out var current, out var next);
        currentStatText.text = configured ? "현재 " + current : "효과 연결 예정";
        nextStatText.text = configured ? "연구 후 " + next : "";
        specialEffectText.text = configured ? GetEffectSummary(item) : "";
        if (itemImage != null)
        {
            if (item.icon != null)
            {
                itemImage.sprite = item.icon;
                itemImage.color = Color.white;
            }
            else
            {
                itemImage.sprite = null;
                itemImage.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            }
        }

        RefreshButton();

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnClickUpgrade);
        }
    }

    void RefreshButton()
    {
        if (currentItem == null || researchUI == null) return;
        if (upgradeButton != null) upgradeButton.interactable = researchUI.CanUpgrade(currentItem);
        if (upgradeButtonText != null)
            upgradeButtonText.text = researchUI.GetResearchButtonText(currentItem);
        if (specialEffectText != null && currentItem.IsConfigured)
        {
            if (researchUI.ActiveResearch == currentItem)
                specialEffectText.text = $"남은 {researchUI.RemainingResearchTime:0.0}초\n" +
                    (Time.timeScale <= 0f ? "메뉴 닫으면 진행" : "완료 시 전체 강화");
            else if (currentItem.level >= currentItem.MaxResearchLevel)
                specialEffectText.text = GetEffectSummary(currentItem);
            else
                specialEffectText.text = $"소요 {researchUI.GetResearchDuration(currentItem):0.#}초 · 전체 강화\n이후 건설에도 적용";
        }
    }

    static string GetEffectSummary(ResearchItem item) => item.IsWallReinforcement
        ? "모든 방어벽 강화\n이후 건설에도 적용"
        : "동일 종류 전체 강화\n이후 건설에도 적용";

    void OnClickUpgrade()
    {
        if (researchUI == null || currentItem == null) return;

        researchUI.UpgradeItem(currentItem);
    }
}