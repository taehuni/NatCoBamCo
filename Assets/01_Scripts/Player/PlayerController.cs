using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("플레이어 체력")]
    public int maxHp = 100;
    public int curHp;
    [Header("플레이어 이동")]
    public float moveSpeed = 5f; //이동 속도
    [Header("플레이어 점프")]
    public float jumpForce = 7f; //점프 힘
    public float gravity = -20f; //중력
    [Header("카메라")]
    public Camera mainCam; //메인 카메라
    [Header("플레이어 사격")]
    public PlayerShoot playerShoot;
    
    private float verticalVelocity;
    private CharacterController controller; //자기의 CharacterController

    void Start()
    {
        playerShoot = GetComponent<PlayerShoot>();
        controller = GetComponent<CharacterController>(); //자기의 CharacterController가져와
        mainCam = Camera.main; //메인 카메라 가져와
        curHp = maxHp;
    }

    void Update()
    {
        PlayerRun();
        PlayerMoveAndRotate();

    }

    //플레이어 이동 + 회전
    public void PlayerMoveAndRotate()
    {
        //카메라의 좌우 이동을 플레이어한테 반응
        float cameraY = mainCam.transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, cameraY, 0f);

        //상하좌우 이동 받기
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        //카메라에 방향 가져와 앞,우
        Vector3 forward = mainCam.transform.forward;
        Vector3 right = mainCam.transform.right;

        //y측 값이 없음(하늘에 올아가기 제한)
        forward.y = 0f;
        right.y = 0f;
        //초기화
        forward.Normalize();
        right.Normalize();

        //이동방향 계산
        Vector3 moveDir = (forward * vertical + right * horizontal).normalized;


        if (controller.isGrounded)
        {
            //작은 종력이 주고 지면에 안정하게 있게
            verticalVelocity = -1f;

            //space 누를 때
            if (Input.GetKeyDown(KeyCode.Space) && !playerShoot.isPrecisionMode)
            {
                verticalVelocity = jumpForce;
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 finalMove = moveDir * moveSpeed;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);
    }

    // 传送由玩家移动脚本处理：临时关闭碰撞控制器，并清空上一场景的跳跃/下落速度。
    // 전송은 플레이어 이동 스크립트에서 처리한다. 충돌 컨트롤러를 잠시 끄고 이전 씬의 점프/낙하 속도를 초기화한다.
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        // 场景加载事件可能早于 Start，因此这里也允许获取控制器。
        // 씬 로드 이벤트가 Start보다 먼저 올 수 있으므로 여기서도 컨트롤러를 가져올 수 있게 한다.
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        bool wasEnabled = controller != null && controller.enabled;

        if (wasEnabled)
        {
            controller.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);
        verticalVelocity = 0f;

        if (wasEnabled)
        {
            controller.enabled = true;
        }
    }

    public void PlayerRun()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            moveSpeed = 10f;
        }
        else
        {
            moveSpeed = 5f;
        }
    }

    public void Heal(int amount)
    {
        curHp += amount;

        if (curHp > maxHp)
            curHp = maxHp;

        //이후 기능이 추가된다면 아래에 해당 기능 추가.
        // 체력 UI 갱신 
        // 힐 이펙트
    }
}
