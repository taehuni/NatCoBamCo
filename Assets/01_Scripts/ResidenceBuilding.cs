using UnityEngine;
using UnityEngine.UI;

public class ResidenceBuilding : MonoBehaviour
{
    [Header("상호작용")]
    public float detectRange = 4f;
    public LayerMask playerLayer;

    private PlayerInteractUI playerUI;

    private bool playerInRange;

    void Update()
    {
        CheckPlayerNear();

        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            OpenResidence();
        }
    }

    void CheckPlayerNear()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, detectRange, playerLayer);

        playerInRange = players.Length > 0;

        if (playerInRange)
        {
            playerUI = players[0].GetComponentInParent<PlayerInteractUI>();

            if (playerUI != null)
                playerUI.ShowButton("거주구역(E)");
        }
        else
        {
            if (playerUI != null)
                playerUI.HideButton();

            playerUI = null;
        }
    }

    void OpenResidence()
    {
        Debug.Log("거주구역 UI 오픈");

        // TODO : 거주구역 UI 열기
    }

    public void UpgradeSurvivor()
    {
        Debug.Log("생존자 능력 강화");

        // TODO : 생존자 능력 강화
    }

    public void AssignMission()
    {
        Debug.Log("생존자 임무 부여");

        // TODO : 생존자에게 임무 부여
    }

    public void SendGathering()
    {
        Debug.Log("생존자를 채집 보내기");

        // TODO : 낮 동안 채집 시작
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
