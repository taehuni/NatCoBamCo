using UnityEngine;

public enum ResearchCategory
{
    Wall,
    Tower
}

[System.Serializable]
public class ResearchItem
{
    public ResearchCategory category;

    [Header("기본 정보")]
    public string itemName;
    public Sprite icon;

    [TextArea]
    public string costText;

    [TextArea]
    public string currentStatText;

    [TextArea]
    public string nextStatText;

    [TextArea]
    public string specialEffectText;

    public int level = 0;
}