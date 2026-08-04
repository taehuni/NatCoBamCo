using UnityEngine;

public class ResearchInteract : MonoBehaviour
{
    public ResearchUI researchUI;
    public string playerTag = "Player";

    private bool isPlayerNear = false;

    void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E키 입력됨 - 연구소 UI 열기 시도");

            if (researchUI != null)
            {
                researchUI.OpenUI();
            }
            else
            {
                Debug.LogError("ResearchUI가 연결되지 않았습니다.");
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter: " + other.name);

        if (other.CompareTag(playerTag))
        {
            Debug.Log("플레이어가 연구소 범위에 들어옴");
            isPlayerNear = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exit: " + other.name);

        if (other.CompareTag(playerTag))
        {
            Debug.Log("플레이어가 연구소 범위에서 나감");
            isPlayerNear = false;
        }
    }
}