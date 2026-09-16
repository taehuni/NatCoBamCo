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

    public void Setup(ResearchItem item, ResearchUI ui)
    {
        currentItem = item;
        researchUI = ui;

        nameText.text = item.itemName + " Lv." + item.level;
        costText.text = item.costText;
        currentStatText.text = item.currentStatText;
        nextStatText.text = item.nextStatText;
        specialEffectText.text = item.specialEffectText;

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

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = "업그레이드";
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnClickUpgrade);
        }
    }

    void OnClickUpgrade()
    {
        if (researchUI == null || currentItem == null) return;

        researchUI.UpgradeItem(currentItem);
    }
}