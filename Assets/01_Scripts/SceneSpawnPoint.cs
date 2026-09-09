using UnityEngine;

// 挂在目标场景的空对象上，用该对象的 Transform 标记玩家出生位置。
// 목적지 씬의 빈 오브젝트에 붙여 Transform으로 플레이어 생성 위치를 표시한다.
[DisallowMultipleComponent]
public class SceneSpawnPoint : MonoBehaviour
{
    // 仅在 Scene 视图显示位置标记，不会出现在游戏画面中。
    // Scene 뷰에만 위치 표시를 그리며 게임 화면에는 나타나지 않는다.
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 position = transform.position;
        Gizmos.DrawWireSphere(position, 0.3f);
        Gizmos.DrawLine(position - Vector3.right * 0.5f, position + Vector3.right * 0.5f);
        Gizmos.DrawLine(position - Vector3.forward * 0.5f, position + Vector3.forward * 0.5f);
    }
}
