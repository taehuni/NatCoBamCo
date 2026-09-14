using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keep completed buildings for this play session; inactive scene groups do not simulate.
public class BuiltBuildingPersistence : MonoBehaviour
{
    private static BuiltBuildingPersistence instance;
    private readonly Dictionary<string, GameObject> sceneGroups = new Dictionary<string, GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => instance = null;

    public static void Register(GameObject building)
    {
        if (building == null || !Application.isPlaying) return;
        // Re-registering an already owned building must not move it to another scene group.
        if (building.GetComponentInParent<BuiltBuildingPersistence>(true) != null) return;

        Scene ownerScene = building.scene;
        if (!ownerScene.IsValid() || string.IsNullOrEmpty(ownerScene.path)) return;

        if (instance == null)
            new GameObject("BuiltBuildingPersistence").AddComponent<BuiltBuildingPersistence>();

        if (!instance.sceneGroups.TryGetValue(ownerScene.path, out var group) || group == null)
        {
            group = new GameObject("Buildings_" + ownerScene.name);
            group.transform.SetParent(instance.transform, false);
            instance.sceneGroups[ownerScene.path] = group;
        }

        // The caller's inactive staging parent prevents OnEnable until ownership is ready.
        building.transform.SetParent(group.transform, true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (sceneGroups.TryGetValue(scene.path, out var group) && group != null)
            group.SetActive(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        // Returning to the title starts a fresh construction session.
        if (scene.name == "00_Intro")
        {
            foreach (var group in sceneGroups.Values)
            {
                if (group == null) continue;
                group.SetActive(false);
                Destroy(group);
            }
            sceneGroups.Clear();
            return;
        }

        foreach (var entry in sceneGroups)
            if (entry.Value != null) entry.Value.SetActive(entry.Key == scene.path);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (instance == this) instance = null;
    }
}
