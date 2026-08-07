using UnityEngine;

public enum BuildCategory
{
    Wall,
    Tower,
    Building
}

[System.Serializable]
public class BuildItem
{
    [Header("카테고리")]
    public BuildCategory category;

    [Header("기본 정보")]
    public string itemName;
    public Sprite icon;

    [Header("설치 프리팹")]
    public GameObject buildPrefab;

    [Header("UI 텍스트")]
    [TextArea]
    public string costText;

    [TextArea]
    public string descriptionText;
}