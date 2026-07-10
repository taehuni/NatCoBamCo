using UnityEngine;

public class PlayerController : MonoBehaviour
{   
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
}