using UnityEngine;
using UnityEngine.UI;

// Display only: collection and spending remain owned by ResourceInventory.
public sealed class ResourceCounterUI : MonoBehaviour
{
    public ResourceInventory inventory;
    public Text woodText;
    public Text metalText;
    public Text rareMetalText;
    public Text foodText;

    private ResourceInventory subscribedInventory;

    void OnEnable() => Bind(inventory != null ? inventory : ResourceInventory.Instance);

    void Start()
    {
        if (subscribedInventory == null)
            Bind(inventory != null ? inventory : ResourceInventory.Instance);
    }

    void OnDisable()
    {
        if (subscribedInventory != null) subscribedInventory.Changed -= Refresh;
        subscribedInventory = null;
    }

    public void Bind(ResourceInventory source)
    {
        if (subscribedInventory != null) subscribedInventory.Changed -= Refresh;
        inventory = source;
        subscribedInventory = source;
        if (subscribedInventory != null) subscribedInventory.Changed += Refresh;
        Refresh();
    }

    void Refresh()
    {
        Set(woodText, "목재", ResourceType.Wood);
        Set(metalText, "금속", ResourceType.Metal);
        Set(rareMetalText, "희귀 금속", ResourceType.RareMetal);
        Set(foodText, "식량", ResourceType.Food);
    }

    void Set(Text label, string name, ResourceType type)
    {
        if (label != null) label.text = name + " " + (inventory != null ? inventory.Get(type).ToString() : "—");
    }
}
