using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerScenePersistence : MonoBehaviour
{
    private static PlayerScenePersistence instance;
    private PlayerController playerController;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            // 新场景自带的重复玩家立即停用，避免销毁前仍响应输入或场景事件。
            // 새 씬의 중복 플레이어는 즉시 비활성화해 파괴 전 입력이나 씬 이벤트에 반응하지 않게 한다.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        instance = this;
        playerController = GetComponent<PlayerController>();

        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 普通场景切换才重新放置玩家，附加加载的场景不触发传送。
        // 일반 씬 전환에만 플레이어를 배치하며 추가 로드한 씬은 전송을 일으키지 않는다.
        if (instance != this || mode != LoadSceneMode.Single)
        {
            return;
        }

        SceneSpawnPoint spawnPoint = FindSpawnPoint(scene);

        if (spawnPoint == null)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.TeleportTo(spawnPoint.transform.position, spawnPoint.transform.rotation);
        }
        else
        {
            transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);
        }
    }

    // 只检查刚加载的目标场景，出生点可以放在场景中的普通父对象下面。
    // 방금 로드한 목적지 씬만 확인하며 생성 지점은 씬의 일반 부모 오브젝트 아래에 둘 수 있다.
    SceneSpawnPoint FindSpawnPoint(Scene scene)
    {
        SceneSpawnPoint foundPoint = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SceneSpawnPoint candidate in root.GetComponentsInChildren<SceneSpawnPoint>())
            {
                if (!candidate.isActiveAndEnabled)
                {
                    continue;
                }

                if (foundPoint != null)
                {
                    Debug.LogWarning(
                        $"Scene '{scene.name}' has multiple active SceneSpawnPoint components. Keep exactly one. Player position was not changed.",
                        this
                    );
                    return null;
                }

                foundPoint = candidate;
            }
        }

        if (foundPoint == null)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' has no active SceneSpawnPoint. Add it to an empty GameObject at the player spawn position. Player position was not changed.",
                this
            );
        }

        return foundPoint;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
