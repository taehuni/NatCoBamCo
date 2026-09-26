using UnityEngine;

public class Wall : MonoBehaviour
{
    public const float ResearchHealthBonusPercent = 30f;
    public const float ResearchDefenseBonus = 5f;

    [SerializeField, HideInInspector] private bool reinforcementApplied;

    // Each wall keeps its own application state through normal scene transitions.
    public void ApplyReinforcement()
    {
        if (reinforcementApplied || gameObject.layer == LayerMask.NameToLayer("Ignore Raycast") ||
            !TryGetComponent<DamageableBuilding>(out var building)) return;

        var health = building.Health; // Also completes legacy prefab health migration before building Awake.
        if (!health.IsAlive) return;

        float missingHealth = health.MaxHealth - health.CurrentHealth;
        float maximum = health.MaxHealth + health.MaxHealth * ResearchHealthBonusPercent / 100f;
        health.SetHealthValues(maximum, maximum - missingHealth);
        building.Defense.SetDefensePower(building.defensePower + ResearchDefenseBonus);
        reinforcementApplied = true;
    }
}
