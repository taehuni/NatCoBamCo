using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 태훈 추가: 씬 전환 감지용

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (SurvivorAI survivor in roster)
        {
            if (survivor == null)
            {
                continue;
            }

            survivor.homePoint = FindHomePointForRole(survivor.role);
            survivor.GoHome();
        }
    }

    public void AddSurvivor(SurvivorAI survivor)
    {
        if (survivor == null || roster.Contains(survivor))
        {
            return;
        }

        if (survivor.homePoint == null)
        {
            survivor.homePoint = FindHomePointForRole(survivor.role);
        }

        roster.Add(survivor);

        // 태훈 추가: 매니저(DontDestroyOnLoad) 밑으로 옮겨서 생존자 본체도 씬 전환 시 같이 유지되게 함
        survivor.transform.SetParent(transform);

        survivor.GoHome();

        Debug.Log($"{survivor.survivorName} 생존자 합류 ({survivor.role})");
    }

    // 채집가 -> ResidenceBuilding 위치, 연구원 -> ResearchLab 위치, 정비공 -> 없음(직접 이동하므로 불필요)
    Transform FindHomePointForRole(SurvivorAI.SurvivorRole role)
    {
        if (role == SurvivorAI.SurvivorRole.Researcher)
        {
            ResearchLab lab = FindObjectOfType<ResearchLab>();
            return lab != null ? lab.transform : null;
        }

        // 태훈 수정: 정비공도 낮에는 거주구역 근처를 배회하도록 홈포인트 부여 (원래는 Gatherer만 해당, Mechanic은 null)
        ResidenceBuilding residence = FindObjectOfType<ResidenceBuilding>();
        return residence != null ? residence.transform : null;
    }

    // 채집가가 로스터에 있고 부상이 아니면 합산 (자원 획득량 배율 보너스)
    public float GetGatherBonus()
    {
        float total = 0f;

        foreach (SurvivorAI s in roster)
        {
            if (s.role == SurvivorAI.SurvivorRole.Gatherer && s.IsAvailable)
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
            if (s.role == SurvivorAI.SurvivorRole.Researcher && s.IsAvailable)
            {
                reduction += s.researchSpeedBonus;
            }
        }

        reduction = Mathf.Clamp(reduction, 0f, 0.9f);
        return 1f - reduction;
    }
}
