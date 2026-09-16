using UnityEngine;
using UnityEngine.EventSystems;

public class ResearchInteract : MonoBehaviour
{
    public ResearchUI researchUI;

    void Awake()
    {
        ResolveResearchUI();
    }

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        ResolveResearchUI();

        if (researchUI != null)
        {
            researchUI.OpenUI();
        }
        else
        {
            Debug.LogError("ResearchUI가 장면에 없습니다.");
        }
    }

    void ResolveResearchUI()
    {
        if (researchUI == null)
        {
            researchUI = FindFirstObjectByType<ResearchUI>(FindObjectsInactive.Include);
        }
    }
}
