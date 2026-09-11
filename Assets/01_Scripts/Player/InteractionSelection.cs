using System.Collections.Generic;
using UnityEngine;

// Selection controls which action may start. Each target still owns its effects
// and decides when its ongoing action ends.
public interface IInteractionTarget
{
    KeyCode InteractionKey { get; }
    bool CanBeginInteraction { get; }
    bool InteractionInProgress { get; }
    bool IsPlayerInInteractionRange(PlayerController player);
}

public static class InteractionSelection
{
    private sealed class KeyPress
    {
        public MonoBehaviour target;
        public bool busy;
        public bool consumed;
    }

    private static readonly HashSet<MonoBehaviour> targets = new HashSet<MonoBehaviour>();
    private static readonly Dictionary<(PlayerController, KeyCode), KeyPress> presses =
        new Dictionary<(PlayerController, KeyCode), KeyPress>();
    private static readonly Dictionary<PlayerController, Collider[]> playerColliders =
        new Dictionary<PlayerController, Collider[]>();
    private static int frame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        targets.Clear();
        presses.Clear();
        playerColliders.Clear();
        frame = -1;
    }

    public static void Register(MonoBehaviour target)
    {
        if (target is IInteractionTarget) targets.Add(target);
    }

    public static void Unregister(MonoBehaviour target) => targets.Remove(target);

    private static void RefreshFrame()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        presses.Clear();
        playerColliders.Clear();
    }

    public static bool IsInRange(MonoBehaviour target, PlayerController player, float range, LayerMask mask)
    {
        if (target == null || player == null || !player.gameObject.activeInHierarchy || range < 0f) return false;
        RefreshFrame();
        if (!playerColliders.TryGetValue(player, out var colliders))
        {
            colliders = player.GetComponentsInChildren<Collider>();
            playerColliders.Add(player, colliders);
        }

        foreach (var collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                (mask.value & (1 << collider.gameObject.layer)) == 0) continue;
            var closest = collider.ClosestPoint(target.transform.position);
            if ((closest - target.transform.position).sqrMagnitude <= range * range) return true;
        }
        return false;
    }

    public static bool TryBegin(MonoBehaviour target, PlayerController player)
    {
        if (target == null || player == null || !(target is IInteractionTarget action)) return false;
        RefreshFrame();
        var key = (player, action.InteractionKey);
        if (!presses.TryGetValue(key, out var press))
        {
            var selected = Choose(player, action.InteractionKey, false);
            press = new KeyPress
            {
                target = selected,
                busy = selected != null && ((IInteractionTarget)selected).InteractionInProgress
            };
            presses.Add(key, press);
        }

        // Keep the result for the entire key-down frame, even if the winner
        // completes or disables itself before another target's Update runs.
        if (press.consumed || press.busy || press.target != target ||
            !target.isActiveAndEnabled || !action.CanBeginInteraction ||
            !action.IsPlayerInInteractionRange(player)) return false;
        press.consumed = true;
        return true;
    }

    public static MonoBehaviour GetDisplayTarget(PlayerController player) => Choose(player, null, true);

    private static MonoBehaviour Choose(PlayerController player, KeyCode? key, bool showUnavailable)
    {
        if (player == null) return null;
        MonoBehaviour busy = null, ready = null, unavailable = null;
        float busyDistance = float.PositiveInfinity, readyDistance = float.PositiveInfinity,
            unavailableDistance = float.PositiveInfinity;

        foreach (var target in targets)
        {
            if (target == null || !target.isActiveAndEnabled) continue;
            var action = (IInteractionTarget)target;
            if (!action.IsPlayerInInteractionRange(player)) continue;
            float distance = (target.transform.position - player.transform.position).sqrMagnitude;
            if (action.InteractionInProgress)
            {
                PickCloser(target, distance, ref busy, ref busyDistance);
            }
            else if (!key.HasValue || action.InteractionKey == key.Value)
            {
                if (action.CanBeginInteraction)
                    PickCloser(target, distance, ref ready, ref readyDistance);
                else if (showUnavailable)
                    PickCloser(target, distance, ref unavailable, ref unavailableDistance);
            }
        }

        // A running collection/heal stays selected until it ends or leaves range.
        return busy != null ? busy : ready != null ? ready : unavailable;
    }

    private static void PickCloser(MonoBehaviour candidate, float distance,
        ref MonoBehaviour best, ref float bestDistance)
    {
        if (best == null || distance < bestDistance ||
            (distance == bestDistance && candidate.GetInstanceID() < best.GetInstanceID()))
        {
            best = candidate;
            bestDistance = distance;
        }
    }
}
