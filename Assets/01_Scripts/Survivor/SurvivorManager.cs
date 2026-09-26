using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 태훈 추가: 씬 전환 감지용
using UnityEngine.AI;

// 생존자 로스터 관리 싱글턴. 씬에 빈 오브젝트 하나 만들어서 붙이면 됨.
//
// 다른 시스템과의 연결점
//  - ResourceNode(채집): GetGatherBonus() 를 채집량 배율에 더한다.
//  - 연구 시스템(아직 스크립트 없음): GetResearchSpeedMultiplier() 를 연구 소요시간에 곱한다.
//  - 정비공 수리는 SurvivorMechanicBehaviour 가 각자 알아서 처리하므로 여기서는 집계하지 않음.
public class SurvivorManager : MonoBehaviour
{
    public static SurvivorManager Instance { get; private set; }

    [Header("생존자 목록")]
    public List<SurvivorAI> roster = new List<SurvivorAI>();

    [Header("합류할 씬 (비어 있으면 거주 시설이 있는 씬)")]
    public string shelterSceneName;

    private readonly Dictionary<SurvivorAI, (Vector3 position, Quaternion rotation)> shelterPositions =
        new Dictionary<SurvivorAI, (Vector3, Quaternion)>();
    private float nextPlacementRetry;
    private int shelterSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSingleton() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 태훈 추가: 씬 전환해도 구출 명단(roster)이 유지되도록 파괴 방지
        DontDestroyOnLoad(gameObject);
    }

    // 태훈 추가: 씬이 바뀌면 이전 씬의 homePoint(파괴됨)를 새 씬 기준으로 다시 찾아서 이동시킴
    void OnEnable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime < nextPlacementRetry ||
            (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsRestarting))) return;
        nextPlacementRetry = Time.unscaledTime + 1f;
        var scene = SceneManager.GetActiveScene();
        if (!IsShelterScene(scene)) return;
        shelterSceneHandle = scene.handle;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] != null && (!roster[i].gameObject.activeSelf || roster[i].homePoint == null))
                PlaceSurvivor(roster[i], scene, i);
    }

    void OnSceneUnloaded(Scene scene)
    {
        if (scene.handle != shelterSceneHandle) return;
        shelterSceneHandle = -1;
        foreach (var survivor in roster)
        {
            if (survivor == null || !survivor.gameObject.activeSelf) continue;
            shelterPositions[survivor] = (survivor.transform.position, survivor.transform.rotation);
            survivor.gameObject.SetActive(false);
            survivor.homePoint = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this || mode != LoadSceneMode.Single) return;
        if (scene.name == "00_Intro")
        {
            SurvivorRescueEvent.ResetSession();
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        bool isShelter = IsShelterScene(scene);
        if (isShelter) shelterSceneHandle = scene.handle;
        for (int i = 0; i < roster.Count; i++)
        {
            var survivor = roster[i];
            if (survivor == null) continue;
            if (isShelter) PlaceSurvivor(survivor, scene, i);
            else survivor.gameObject.SetActive(false);
        }
    }

    public bool AddSurvivor(SurvivorAI survivor)
    {
        if (survivor == null || roster.Contains(survivor))
        {
            return false;
        }
        survivor.gameObject.SetActive(false);
        survivor.homePoint = null;
        survivor.state = SurvivorAI.SurvivorState.Rescued;
        roster.Add(survivor);

        // 태훈 추가: 매니저(DontDestroyOnLoad) 밑으로 옮겨서 생존자 본체도 씬 전환 시 같이 유지되게 함
        survivor.transform.SetParent(transform);

        Debug.Log($"{survivor.survivorName} 생존자 합류 ({survivor.role})");
        return true;
    }

    bool IsShelterScene(Scene scene) => !string.IsNullOrEmpty(shelterSceneName)
        ? scene.name == shelterSceneName
        : FindHomePointForRole(SurvivorAI.SurvivorRole.Gatherer, scene) != null ||
          FindHomePointForRole(SurvivorAI.SurvivorRole.Researcher, scene) != null;

    // Only use facilities in the newly loaded scene, not retained inactive objects.
    Transform FindHomePointForRole(SurvivorAI.SurvivorRole role, Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (role == SurvivorAI.SurvivorRole.Researcher)
            {
                var lab = root.GetComponentInChildren<ResearchLab>();
                if (lab != null) return lab.transform;
            }
            else
            {
                var residence = root.GetComponentInChildren<ResidenceBuilding>();
                if (residence != null) return residence.transform;
            }
        }
        return null;
    }

    void PlaceSurvivor(SurvivorAI survivor, Scene scene, int index)
    {
        var home = FindHomePointForRole(survivor.role, scene);
        var movement = survivor.GetComponent<SurvivorMovement>();
        if (home == null || movement == null || movement.Agent == null) return;
        var agent = movement.Agent;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 position = home.position;
        Quaternion rotation = survivor.transform.rotation;
        NavMeshHit hit;
        bool found = false;
        if (shelterPositions.TryGetValue(survivor, out var saved))
        {
            found = NavMesh.SamplePosition(saved.position, out hit, 2f, filter);
            if (found) position = hit.position;
            rotation = saved.rotation;
        }
        for (int attempt = 0; !found && attempt < 12; attempt++)
        {
            float angle = (index * 120f + attempt * 30f) * Mathf.Deg2Rad;
            var candidate = home.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 4f;
            found = NavMesh.SamplePosition(candidate, out hit, 2f, filter);
            if (found) position = hit.position;
        }
        if (!found) return; // Retry when the destination NavMesh or facility becomes available.
        agent.enabled = false;
        survivor.transform.SetPositionAndRotation(position, rotation);
        survivor.homePoint = home;
        survivor.gameObject.SetActive(true);
        agent.enabled = true;
        if (!agent.isOnNavMesh || !agent.Warp(position))
        {
            survivor.gameObject.SetActive(false);
            return;
        }
        movement.MoveToPosition(position);
    }

    // 채집가가 로스터에 있고 부상이 아니면 합산 (자원 획득량 배율 보너스)
    public float GetGatherBonus()
    {
        float total = 0f;

        foreach (SurvivorAI s in roster)
        {
            if (s != null && s.state == SurvivorAI.SurvivorState.Rescued &&
                s.role == SurvivorAI.SurvivorRole.Gatherer && s.IsAvailable)
            {
                total += s.gatherBonus;
            }
        }

        return total;
    }

    // 연구원이 로스터에 있고 부상이 아니면 합산 (연구 시간 배율, 0~0.9 감소로 클램프)
    public float GetResearchSpeedMultiplier()
    {
        float reduction = 0f;

        foreach (SurvivorAI s in roster)
        {
            if (s != null && s.state == SurvivorAI.SurvivorState.Rescued &&
                s.role == SurvivorAI.SurvivorRole.Researcher && s.IsAvailable)
            {
                reduction += s.researchSpeedBonus;
            }
        }

        reduction = Mathf.Clamp(reduction, 0f, 0.9f);
        return 1f - reduction;
    }
}
