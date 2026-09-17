using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class BuildingSystem : MonoBehaviour
{
    public Camera mainCam;
    public GameObject wallPrefab; //벽 소재
    public GameObject towerPrefab; //타워 소재
    public GameObject electricTowerPrefab; //전기타워 소재
    private GameObject currentBuildingPrefab; //현재 건조할 건축
    private BuildItem currentBuildItem;
    private int placementStartedFrame = -1;
    private int lastBuildFrame = -1;
    private GameObject previewBuilding; //미리보기 temp
    public float buildDistance = 10f; //건조 범위

    public LayerMask buildRayLayer; //건조 조건 layer
    public bool isBuildMode; //건조 모드
    public float gridSize = 1f; //가이드 사이즈
    public float checkBoxScale = 0.98f; //충돌 판단 크기
    public Vector3 checkBoxHalfSize = new Vector3(0.45f, 0.45f, 0.45f); //충돌 판단 default 크기(대상 못 찾는 경우)
    public LayerMask blockedLayer; //충돌 대상의 layer
    private bool canBuild; //건조 가능할까?
    public Material canBuildMaterial; //건조 가능할때 재질
    public Material cannotBuildMaterial; //건조 불가능할때 재질
    private Vector3 currentBuildPosition; //건조 위치
    public float rotateAngle = 90f; //건조 화전각도
    private float currentRotationY; //현재 각도

    public bool isRemoveMode; //삭제 모드
    public LayerMask removeRayLayer; //삭제 대상의 layer
    private BuildingObject currentRemoveTarget; //현재 삭제할 대상
    private Renderer[] removeTargetRenderers; //대상의 renderers
    private Material[][] removeTargetOriginalMaterials; //원래 대상의 materials




    void Start()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }
        currentBuildingPrefab = wallPrefab; //default wall시작
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CancelPlacement();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CancelPlacement();

    public void CancelPlacement()
    {
        isBuildMode = false;
        isRemoveMode = false;
        canBuild = false;
        currentRotationY = 0f;
        DestroyPreview();
        ClearRemoveTarget();
    }

    void Update()
    {
        HandleModeSwitch(); //모드 전화

        if (isBuildMode)
        {
            if (Time.frameCount != placementStartedFrame)
            {
                HandleBuildingSelection(); //건축 선택
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                RotatePreview(); //미리보기 회전
            }

            UpdatePreview(); // Recheck the rotated footprint before accepting a click.

            bool mouseConfirm = Input.GetMouseButtonDown(0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            bool keyboardConfirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

            if ((mouseConfirm || keyboardConfirm) && canBuild && Time.frameCount != placementStartedFrame)
            {
                TryBuild(); //건조시도
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPlacement();
            }
        }

        if (isRemoveMode) //건조 삭제 모드
        {
            UpdateRemovePreview(); //삭제 대상 재질 변경
            if (Input.GetMouseButtonDown(0))
            {
                TryRemoveBuilding(); //삭제 시도
            }
        }
    }

    //모드전환 건조 - 삭제
    void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.X)) //x키 누르면 삭제 모드 전환
        {
            isRemoveMode = !isRemoveMode;

            if (isRemoveMode)
            {
                isBuildMode = false; //건조 모드 끄기
                currentRotationY = 0f; //회전 초기화
                DestroyPreview(); //미리보기 끄기
            }
            else
            {
                ClearRemoveTarget(); //임시 저장한 삭제대상 초기화
            }
        }
    }

    //삭제 대상 재질 변경함수
    void UpdateRemovePreview()
    {
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, buildDistance, removeRayLayer))
        {
            BuildingObject building = hit.collider.GetComponentInParent<BuildingObject>();

            if (building != null)
            {
                SetRemoveTarget(building);
                return;
            }
        }

        ClearRemoveTarget();
    }

    //삭제 대상의 재질등 내용 임시 저장
    void SetRemoveTarget(BuildingObject building)
    {
        if (currentRemoveTarget == building)
        {
            return;
        }

        ClearRemoveTarget();

        currentRemoveTarget = building;

        removeTargetRenderers = currentRemoveTarget.GetComponentsInChildren<Renderer>();

        removeTargetOriginalMaterials = new Material[removeTargetRenderers.Length][];

        for (int i = 0; i < removeTargetRenderers.Length; i++)
        {
            removeTargetOriginalMaterials[i] = removeTargetRenderers[i].materials;

            Material[] redMaterials = new Material[removeTargetRenderers[i].materials.Length];

            for (int j = 0; j < redMaterials.Length; j++)
            {
                redMaterials[j] = cannotBuildMaterial;
            }

            removeTargetRenderers[i].materials = redMaterials;
        }
    }

    void ClearRemoveTarget()
    {
        if (currentRemoveTarget == null)
        {
            return;
        }

        if (removeTargetRenderers != null && removeTargetOriginalMaterials != null)
        {
            for (int i = 0; i < removeTargetRenderers.Length; i++)
            {
                if (removeTargetRenderers[i] != null)
                {
                    removeTargetRenderers[i].materials = removeTargetOriginalMaterials[i];
                }
            }
        }

        currentRemoveTarget = null;
        removeTargetRenderers = null;
        removeTargetOriginalMaterials = null;
    }

    void TryRemoveBuilding()
    {
        if (currentRemoveTarget == null)
        {
            return;
        }

        Destroy(currentRemoveTarget.gameObject);

        currentRemoveTarget = null;
        removeTargetRenderers = null;
        removeTargetOriginalMaterials = null;
    }


    //건조 한 대상 선택
    void HandleBuildingSelection()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectBuilding(wallPrefab);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectBuilding(towerPrefab);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectBuilding(electricTowerPrefab);
        }
    }

    //선택함수
    void SelectBuilding(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        currentBuildingPrefab = prefab;
        currentBuildItem = FindBuildItem(prefab);

        if (isBuildMode)
        {
            DestroyPreview();
            CreatePreview();
        }
    }

    BuildItem FindBuildItem(GameObject prefab)
    {
        var menu = FindFirstObjectByType<BuildQuickUI>();
        if (menu == null || prefab == null) return null;
        foreach (var list in new[] { menu.wallItems, menu.towerItems, menu.buildingItems })
            foreach (var item in list)
                if (item != null && item.buildPrefab == prefab) return item;
        return null;
    }

    public bool StartPlacement(BuildItem item)
    {
        if (item == null || item.buildPrefab == null || item.cost == null || !item.cost.IsValid) return false;
        ClearRemoveTarget();
        isRemoveMode = false;
        isBuildMode = true;
        currentBuildItem = item;
        currentBuildingPrefab = item.buildPrefab;
        currentRotationY = 0f;
        canBuild = false;
        placementStartedFrame = Time.frameCount;
        DestroyPreview();
        CreatePreview();
        return true;
    }

    // Preserve callers that select a prefab, including the keyboard shortcuts.
    public void StartPlacement(GameObject prefab) => StartPlacement(FindBuildItem(prefab));

    bool CanAffordCurrentBuilding() => currentBuildItem != null && currentBuildItem.cost != null &&
        currentBuildItem.cost.IsValid && ResourceInventory.Instance != null &&
        ResourceInventory.Instance.CanAfford(currentBuildItem.cost.ToResources());


    //그리드 계산 함수
    Vector3 SnapToGrid(Vector3 position)
    {
        float x = Mathf.Round(position.x / gridSize) * gridSize;
        float z = Mathf.Round(position.z / gridSize) * gridSize;

        return new Vector3(x, position.y, z);
    }

    //미리보기 재질 변경함수
    void SetPreviewMaterial(Material material)
    {
        if (previewBuilding == null || material == null)
        {
            return;
        }
        Renderer[] renderers = previewBuilding.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = material;
        }
    }

    //미리보기 생성
    void CreatePreview()
    {
        if (currentBuildingPrefab == null)
        {
            return;
        }

        // Instantiate inactive so a preview never registers interactions or runs gameplay OnEnable.
        var staging = new GameObject("BuildPreviewStaging");
        staging.SetActive(false);
        previewBuilding = Instantiate(currentBuildingPrefab, staging.transform);
        foreach (var behaviour in previewBuilding.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (var obstacle in previewBuilding.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            obstacle.enabled = false;
        foreach (var body in previewBuilding.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
        foreach (var part in previewBuilding.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            part.gameObject.tag = "Untagged";
        }
        foreach (var health in previewBuilding.GetComponentsInChildren<DamageableBuilding>(true))
        {
            health.Health.SetHealthValues(0f, 0f);
            Destroy(health);
        }

        Collider[] colliders = previewBuilding.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        //전기 타워 미리보기 때 기능 비활성화
        ElectricTower[] electricTowers = previewBuilding.GetComponentsInChildren<ElectricTower>();

        for (int i = 0; i < electricTowers.Length; i++)
        {
            electricTowers[i].enabled = false;
        }
        previewBuilding.SetActive(false);
        previewBuilding.transform.SetParent(null, true);
        Destroy(staging);
    }

    //미리보기 삭제
    void DestroyPreview()
    {
        if (previewBuilding != null)
        {
            previewBuilding.SetActive(false);
            Destroy(previewBuilding);
        }
        previewBuilding = null;
    }

    //실시간 미리보기
    void UpdatePreview()
    {
        if (previewBuilding == null || mainCam == null)
        {
            canBuild = false;
            return;
        }

        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, buildDistance, buildRayLayer))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                Vector3 snappedPosition = SnapToGrid(hit.point);

                currentBuildPosition = snappedPosition + Vector3.up * GetBuildingHeightOffset();

                previewBuilding.SetActive(true);

                previewBuilding.transform.position = currentBuildPosition;
                previewBuilding.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);

                canBuild = !IsPositionBlocked() && CanAffordCurrentBuilding();

                UpdatePreviewMaterial();
            }
            else
            {
                canBuild = false;

                previewBuilding.SetActive(false);
            }

        }
        else
        {
            canBuild = false;
            previewBuilding.SetActive(false);

        }

    }

    //미리보기 색깔을 건조가능한 상태에 따라 변경
    void UpdatePreviewMaterial()
    {
        if (canBuild)
        {
            SetPreviewMaterial(canBuildMaterial);
        }
        else
        {
            SetPreviewMaterial(cannotBuildMaterial);
        }
    }

    //간조 시도
    void TryBuild()
    {
        if (!isBuildMode || previewBuilding == null || Time.frameCount == lastBuildFrame) return;
        Physics.SyncTransforms();
        UpdatePreview();
        if (!canBuild || !CanAffordCurrentBuilding()) return;

        Quaternion buildRotation = Quaternion.Euler(0f, currentRotationY, 0f);
        var staging = new GameObject("BuildPlacementStaging");
        staging.SetActive(false);
        var building = Instantiate(currentBuildingPrefab, currentBuildPosition, buildRotation, staging.transform);
        if (building == null || !ResourceInventory.Instance.TrySpend(currentBuildItem.cost.ToResources()))
        {
            Destroy(staging);
            return;
        }
        BuiltBuildingPersistence.Register(building);
        // Also allow placement in unsaved test scenes that cannot be retained by path.
        if (building.transform.parent == staging.transform) building.transform.SetParent(null, true);
        Destroy(staging);
        lastBuildFrame = Time.frameCount;
        Physics.SyncTransforms();
        CancelPlacement();
    }

    //간조 위치 높이 계산함수
    float GetBuildingHeightOffset()
    {
        Renderer renderer = previewBuilding.GetComponentInChildren<Renderer>(true);

        if (renderer == null)
        {
            return 0f;
        }

        return previewBuilding.transform.position.y - renderer.bounds.min.y;
    }

    //건조 위치에서 충돌 판단
    bool IsPositionBlocked()
    {
        BoxCollider box = previewBuilding.GetComponentInChildren<BoxCollider>();

        if (box == null)
        {
            return Physics.CheckBox(
                previewBuilding.transform.position,
                checkBoxHalfSize,
                previewBuilding.transform.rotation,
                blockedLayer,
                QueryTriggerInteraction.Collide
            );
        }

        Vector3 center = box.transform.TransformPoint(box.center);

        Vector3 halfSize = Vector3.Scale(box.size, box.transform.lossyScale) / 2f * checkBoxScale;

        return Physics.CheckBox(
            center,
            halfSize,
            box.transform.rotation,
            blockedLayer,
            QueryTriggerInteraction.Collide
        );
    }

    //미리보기 회전
    void RotatePreview()
    {
        currentRotationY += rotateAngle;

        if (previewBuilding != null)
        {
            previewBuilding.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
        }
    }
}
