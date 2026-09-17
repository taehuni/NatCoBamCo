using System.Collections.Generic;
using UnityEngine;

// The parent remains active while its resource child is depleted.
public class DailyResourceSpawner : MonoBehaviour
{
    [SerializeField] private string spawnerId;
    [SerializeField] private ResourceNode resourceNode;
    [SerializeField] private ResourceType firstDayResource = ResourceType.Metal;

    private sealed class DailyState
    {
        public int day = 1;
        public ResourceType type;
        public bool collected;
        public readonly System.Random random;

        public DailyState(ResourceType firstType, int seed)
        {
            type = firstType;
            random = new System.Random(seed);
        }
    }

    private static readonly ResourceType[] kinds =
        { ResourceType.Wood, ResourceType.Metal, ResourceType.Food, ResourceType.RareMetal };
    private static readonly Dictionary<string, DailyState> states = new Dictionary<string, DailyState>();
    private static System.Random seedSource = new System.Random();
    private DailyState state;
    private int displayedDay;

    public ResourceNode Resource => resourceNode;
    public int SpawnDay => displayedDay;
    public string Id => spawnerId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        states.Clear();
        seedSource = new System.Random();
    }

    private void Start() => RefreshForCurrentDay();
    private void Update() => RefreshForCurrentDay();

    public void RefreshForCurrentDay()
    {
        if (resourceNode == null || string.IsNullOrEmpty(spawnerId)) return;

        int day = GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.currentDay) : 1;
        if (displayedDay == day) return;

        if (state == null)
        {
            string key = gameObject.scene.path + ":" + spawnerId;
            if (!states.TryGetValue(key, out state))
            {
                state = new DailyState(firstDayResource, seedSource.Next());
                states.Add(key, state);
            }
        }

        // Advance even for days spent outside Collection. Each roll excludes yesterday's kind.
        while (state.day < day)
        {
            int previous = System.Array.IndexOf(kinds, state.type);
            state.type = kinds[(previous + state.random.Next(1, kinds.Length)) % kinds.Length];
            state.day++;
            state.collected = false;
        }

        // Disabling also cancels any collection that was in progress at the day boundary.
        resourceNode.gameObject.SetActive(false);
        resourceNode.resourceType = state.type;
        displayedDay = day;
        resourceNode.gameObject.SetActive(!state.collected);
    }

    public bool CanCollect(ResourceNode node)
    {
        int startedDay = displayedDay;
        RefreshForCurrentDay();
        return node == resourceNode && state != null && !state.collected &&
            startedDay == displayedDay && node.gameObject.activeInHierarchy;
    }

    public void MarkCollected(ResourceNode node)
    {
        if (node != resourceNode || state == null) return;
        state.collected = true;
        resourceNode.gameObject.SetActive(false);
    }
}
