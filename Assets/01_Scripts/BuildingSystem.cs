using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    public Camera mainCam;
    public GameObject wallPrefab; //벽 소재
    public GameObject towerPrefab; //타워 소재
    public GameObject electricTowerPrefab; //전기타워 소재
    private GameObject currentBuildingPrefab; //현재 건조할 건축
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

    void Update()
    {
        HandleModeSwitch(); //모드 전화

        if (isBuildMode)
        {
            HandleBuildingSelection(); //건축 선택
            UpdatePreview(); //미리보기

            if (Input.GetKeyDown(KeyCode.R))
            {
                RotatePreview(); //미리보기 회전
            }

            if (Input.GetMouseButtonDown(0) && canBuild)
            {
                TryBuild(); //건조시도
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
        if (Input.GetKeyDown(KeyCode.B))  //b키 누르면 건조모드 전환
        {
            isBuildMode = !isBuildMode;

            if (isBuildMode)
            {
                isRemoveMode = false; //삭제 모드 끄기
                ClearRemoveTarget(); //삭제 모드 재질 전화 끄기
                CreatePreview(); //미리보기 만들어
            }
            else
            {
                currentRotationY = 0f; //회전 초기화
                DestroyPreview(); //미리보기 끄기
            }
        }

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

        if (isBuildMode)
        {
            DestroyPreview();
            CreatePreview();
        }
    }


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

        previewBuilding = Instantiate(currentBuildingPrefab);

        Collider[] colliders = previewBuilding.GetComponentsInChildren<Collider>();

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
    }

    //미리보기 삭제
    void DestroyPreview()
    {
        if (previewBuilding != null)
        {
            Destroy(previewBuilding);
        }
        previewBuilding = null;
    }

    //실시간 미리보기
    void UpdatePreview()
    {
        if (previewBuilding == null)
        {
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

                canBuild = !IsPositionBlocked();

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
        Quaternion buildRotation = Quaternion.Euler(0f, currentRotationY, 0f);
        Instantiate(currentBuildingPrefab, currentBuildPosition, buildRotation);
    }

    //간조 위치 높이 계산함수
    float GetBuildingHeightOffset()
    {
        Renderer renderer = previewBuilding.GetComponentInChildren<Renderer>();

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