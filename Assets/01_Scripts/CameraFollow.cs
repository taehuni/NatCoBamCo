using UnityEngine;

// Publish this frame's look direction before PlayerController reads it.
[DefaultExecutionOrder(-100)]
public class CameraFollow : MonoBehaviour
{
    public Transform target; //목표(플레이어)
    public float distance = 3.5f; //거리
    public float height = 0.9f; //높이
    public float shoulderOffset = 1.5f; //좌우 시각 거리
    public float mouseSensitivity = 3f; //마우스 속도
    public float minPitch = -20f; //최소 시각 각도
    public float maxPitch = 60f; //최대 시각 각도

    private float yaw; //상하
    private float pitch; //좌우

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        target = player != null ? player.transform : null;

        yaw = transform.eulerAngles.y; //자기의 rotation.y

        Cursor.lockState = CursorLockMode.Locked; //마우스 중간의 고정
        Cursor.visible = false; //마우스 숨기다
    }

    void Update()
    {
        UpdateLookRotation();
    }

    void LateUpdate()
    {
        UpdateFollowPosition();
    }

    public void targetFollow()
    {
        UpdateLookRotation();
        UpdateFollowPosition();
    }

    void UpdateLookRotation()
    {
        if (target == null) return;
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity; //마우스 좌우 이동 값
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity; //마우스 상하 이동 값
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch); //상하이동 제한

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f); //각도 계산
    }

    void UpdateFollowPosition()
    {
        if (target == null) return;

        Vector3 pivot = target.position + Vector3.up * height;

        Vector3 offset = new Vector3(
            shoulderOffset,
            0f,
            -distance
        );

        transform.position = pivot + transform.rotation * offset;
    }
}
