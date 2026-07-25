using System.Collections.Generic;
using UnityEngine;

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
        survivor.GoHome();

        Debug.Log($"{survivor.survivorName} 생존자 합류 ({survivor.role})");
    }

    // 채집가 -> ResidenceBuilding 위치, 연구원 -> ResearchLab 위치, 정비공 -> 없음(직접 이동하므로 불필요)
    Transform FindHomePointForRole(SurvivorAI.SurvivorRole role)
    {
        if (role == SurvivorAI.SurvivorRole.Gatherer)
        {
            ResidenceBuilding residence = FindObjectOfType<ResidenceBuilding>();
            return residence != null ? residence.transform : null;
        }

        if (role == SurvivorAI.SurvivorRole.Researcher)
        {
            ResearchLab lab = FindObjectOfType<ResearchLab>();
            return lab != null ? lab.transform : null;
        }

        return null;
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
