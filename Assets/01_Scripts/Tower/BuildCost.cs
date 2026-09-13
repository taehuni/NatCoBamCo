using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuildCost
{
    public bool configured;
    [Min(0)] public int wood;
    [Min(0)] public int metal;
    [Min(0)] public int rareMetal;
    [Min(0)] public int food;

    public bool IsValid => configured && wood >= 0 && metal >= 0 && rareMetal >= 0 && food >= 0;

    public Dictionary<ResourceType, int> ToResources() => new Dictionary<ResourceType, int>
    {
        { ResourceType.Wood, wood },
        { ResourceType.Metal, metal },
        { ResourceType.RareMetal, rareMetal },
        { ResourceType.Food, food }
    };

    public string DisplayText()
    {
        var parts = new List<string>();
        if (wood > 0) parts.Add("목재 " + wood);
        if (metal > 0) parts.Add("금속 " + metal);
        if (rareMetal > 0) parts.Add("희귀 금속 " + rareMetal);
        if (food > 0) parts.Add("식량 " + food);
        return "소모 자원: " + (parts.Count > 0 ? string.Join(" / ", parts) : "없음");
    }
}
