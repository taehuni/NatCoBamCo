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
    [Header("Research binding")]
    public GameObject targetPrefab;
    public BuildCost cost;
    [Min(0.1f)] public float researchDuration = 30f;

    public int BaseTowerLevel => targetPrefab == null ? 0 :
        targetPrefab.TryGetComponent<ElectricTower>(out var electric) ? electric.level :
        targetPrefab.TryGetComponent<SniperTower>(out var sniper) ? sniper.level :
        targetPrefab.TryGetComponent<Tower>(out var machinegun) ? machinegun.level : 0;

    public bool IsWallReinforcement => category == ResearchCategory.Wall &&
        targetPrefab != null && targetPrefab.GetComponent<Wall>() != null;

    public int MaxResearchLevel => IsWallReinforcement ? 1 : targetPrefab == null ? 0 :
        targetPrefab.TryGetComponent<ElectricTower>(out var electric) ? Mathf.Max(0, electric.maxlevel - electric.level) :
        targetPrefab.TryGetComponent<SniperTower>(out var sniper) ? Mathf.Max(0, sniper.maxLevel - sniper.level) :
        targetPrefab.TryGetComponent<Tower>(out var machinegun) ? Mathf.Max(0, machinegun.maxLevel - machinegun.level) : 0;

    public bool IsConfigured => (category == ResearchCategory.Tower || IsWallReinforcement) && MaxResearchLevel > 0 &&
        cost != null && cost.IsValid;

    // Apply only missing levels, so returning to a scene cannot duplicate bonuses.
    public void ApplyTo(GameObject building)
    {
        if (!IsConfigured || building == null || level <= 0) return;
        if (IsWallReinforcement)
        {
            if (building.TryGetComponent<Wall>(out var wall)) wall.ApplyReinforcement();
            return;
        }
        int targetLevel = BaseTowerLevel + Mathf.Clamp(level, 0, MaxResearchLevel);
        if (targetPrefab.GetComponent<ElectricTower>() != null &&
            building.TryGetComponent<ElectricTower>(out var electric))
        {
            while (electric.level < targetLevel && electric.level < electric.maxlevel)
                electric.LevelUp();
        }
        else if (targetPrefab.GetComponent<SniperTower>() != null &&
                 building.TryGetComponent<SniperTower>(out var sniper))
        {
            while (sniper.level < targetLevel && sniper.level < sniper.maxLevel)
                sniper.LevelUp();
        }
        else if (targetPrefab.GetComponent<Tower>() != null &&
                 building.TryGetComponent<Tower>(out var machinegun))
        {
            while (machinegun.level < targetLevel && machinegun.level < machinegun.maxLevel)
                machinegun.LevelUp();
        }
    }

    // Use the real LevelUp on an inactive component copy, never the prefab asset.
    public void GetStatDescriptions(out string current, out string next)
    {
        current = next = "준비 중";
        if (!IsConfigured) return;
        if (IsWallReinforcement)
        {
            current = DescribeWall(level > 0);
            next = level >= MaxResearchLevel ? "최대 단계 도달" : DescribeWall(true);
            return;
        }
        var preview = new GameObject("ResearchStatPreview");
        preview.SetActive(false);
        try
        {
            if (targetPrefab.TryGetComponent<ElectricTower>(out var electric))
            {
                var copy = preview.AddComponent<ElectricTower>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(electric), copy);
                ApplyTo(preview);
                current = Describe(copy);
                copy.LevelUp();
                next = Describe(copy);
            }
            else if (targetPrefab.TryGetComponent<SniperTower>(out var sniper))
            {
                var copy = preview.AddComponent<SniperTower>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sniper), copy);
                ApplyTo(preview);
                current = Describe(copy);
                copy.LevelUp();
                next = Describe(copy);
            }
            else if (targetPrefab.TryGetComponent<Tower>(out var machinegun))
            {
                var copy = preview.AddComponent<Tower>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(machinegun), copy);
                ApplyTo(preview);
                current = Describe(copy);
                copy.LevelUp();
                next = Describe(copy);
            }
            if (level >= MaxResearchLevel) next = "최대 단계 도달";
        }
        finally { Object.Destroy(preview); }
    }

    static string Describe(Tower tower) =>
        $"Lv.{tower.level} · 공격력 {tower.damage}\n사거리 {tower.attackRange:0.#}\n공격 간격 {tower.attackInterval:0.##}초";

    static string DescribeWall(bool reinforced) =>
        $"벽 전체\n최대 체력 +{(reinforced ? Wall.ResearchHealthBonusPercent : 0):0.#}%\n" +
        $"방어력 +{(reinforced ? Wall.ResearchDefenseBonus : 0):0.#}";

    static string Describe(ElectricTower tower) =>
        $"Lv.{tower.level} · 공격력 {tower.damage}\n사거리 {tower.attackRange:0.#} · 간격 {tower.attackInterval:0.##}초\n" +
        $"감속 {tower.coldPower * 100:0.#}% / {tower.coldTime:0.#}초";

    static string Describe(SniperTower tower) =>
        $"Lv.{tower.level} · 공격력 {tower.damage:0.#}\n사거리 {tower.attackRange:0.#} · 간격 {tower.attackInterval:0.##}초\n" +
        $"치명타 {tower.criticalChance * 100:0.#}% / {tower.criticalMultiplier:0.##}배";
}