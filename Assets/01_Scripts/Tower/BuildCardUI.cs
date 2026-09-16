using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildCardUI : MonoBehaviour
{
    [Header("UI 연결")]
    public Image itemImage;
    public TMP_Text nameText;
    public TMP_Text costText;
    public TMP_Text descriptionText;
    public Button placeButton;
    public TMP_Text placeButtonText;

    private BuildItem currentItem;
    private BuildQuickUI buildUI;

    public void Setup(BuildItem item, BuildQuickUI ui)
    {
        currentItem = item;
        buildUI = ui;

        if (nameText != null)
            nameText.text = item.itemName;

        if (costText != null)
            costText.text = item.costText;

        if (descriptionText != null)
            descriptionText.text = item.descriptionText;

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

        if (placeButtonText != null)
            placeButtonText.text = "설치";

        if (placeButton != null)
        {
            placeButton.onClick.RemoveAllListeners();
            placeButton.onClick.AddListener(OnClickPlace);
        }
    }

    void OnClickPlace()
    {
        if (buildUI == null || currentItem == null) return;

        buildUI.SelectBuildItem(currentItem);
    }
}