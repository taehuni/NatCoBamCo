# 임시 플레이어·쉘터 V1

2026-09-26, feature/Min. 실제 3D 모델을 적용한 플레이 가능한 임시 외형이다. 디자이너의 최종 맵을 받기 전 크기·동선·시설 배치를 확인하는 용도다.

후속 미감 방향: 사용자가 제공한 디자이너 이미지의 낡은 벽돌과 콘크리트 기둥·상단 마감 조합을 쉘터 외벽에도 적용한다. 현재 콘크리트+철제 펜스는 임시 버전이며 해당 방향으로 교체하는 작업은 아직 하지 않았다.

## 확인 방법

1. `Assets/00_Scenes/01_Main.unity`를 열고 Play한다.
2. 기존 이동·달리기 조작으로 대기/걷기/달리기 전환과 네 출입구 통과를 확인한다.
3. 남동쪽 COLLECTION 단말에서 기존 E 상호작용으로 Collection에 이동한다. 돌아오면 같은 플레이어 외형이 유지된다.
4. 기존 건설 메뉴와 연구소를 사용한다. 작업장 모델은 배경이며 새 제작 기능은 없다.

## 구성

| 항목 | 내용 |
| --- | --- |
| 플레이어 | 약 1.86m, 후드·고글·방독 마스크·배낭·소총, 16본 Generic 리그 |
| 애니메이션 | Idle / Walk / Run, MoveSpeed 0 / 5 / 10, Root Motion 미사용 |
| 쉘터 내부 | 48×48m, 사방 출입구 폭 8m, 기존 100m 바닥 유지 |
| 시설 | 중앙 코어, 남서 거주 시설, 북서 연구소, 북동 작업장, 남동 수집 단말 |
| 배경 소품 | 외벽·펜스, 콘크리트 방벽, 보급 상자, 가로등, 통로·건설 공간 표시 |
| 적 스폰 | 사방 중심으로부터 34m, 기존 수·웨이브·간격 유지 |

모델·프리팹은 Survivor, CoreGenerator, WorkshopModule, ResearchCabin, ResidenceModule, PerimeterWall, Barricade, SupplyCrate, LightMast 총 9종이다. 외부 구매/다운로드 에셋은 사용하지 않았다.

## 파일과 교체 구조

- 게임용 FBX·URP 재질·프리팹·Animator·NavMesh: `Assets/04_Assets/PrototypeShelterV1/`
- Blender 편집 원본: `ArtSource/PrototypeShelterV1/PrototypeShelterV1.blend`
- 이미지: `ArtSource/PrototypeShelterV1/Previews/`의 Survivor.png(Blender 스튜디오 렌더), Shelter.png(Unity 전체 배치), MainPlay.png(Unity Play 화면).
- 제작 도구: `Tools/Art/`. Assets 밖에 있어 게임 런타임 스크립트로 포함되지 않는다.

기존 코어·거주 시설·연구소 루트의 기능 컴포넌트를 유지하고 자식 `PrototypeVisual`에 외형을 연결했다. 이전 외벽 루트 `wall`은 비활성 상태로 보관했다. 새 배경은 `PrototypeShelter_V1` 아래에 있다. 시설 모델을 교체할 때는 외형과 충돌 범위·NavMesh를 함께 맞춘다.

플레이어도 기존 루트 아래 `PrototypeVisual`을 사용한다. PlayerController의 `visualAnimator` 참조와 실제 수평 이동 속도를 MoveSpeed에 전달하는 8줄만 추가했다. 기존 이동·회전·사격 계산은 변경하지 않았다. 모델에 맞춰 Main의 CharacterController/BoxCollider 크기는 조정했다. Collection 씬은 이번 작업에서 변경하지 않았으며 Main의 지속 플레이어를 사용한다.

정적 모델 프리팹은 FBX 축 변환을 자식에 보존하고 루트를 회전 0/스케일 1로 감싼다. FBX 자식의 축·스케일을 임의로 초기화하면 모델이 눕거나 크기가 달라질 수 있다.

## 원본 재생성

이미 적용된 Main에서는 제작 도구를 다시 실행할 필요가 없다. 변경을 보존한 뒤 별도 검토 상황에서만 사용한다. Python 두 파일의 ROOT/root는 로컬 프로젝트 절대 경로이므로 다른 PC에서는 먼저 수정한다.

1. Blender 백그라운드에서 `Tools/Art/build_prototype_shelter.py`를 실행하면 FBX·팔레트·blend 원본을 다시 만든다. 기존 생성 파일을 덮어쓴다.
2. 연결된 Unity 편집기에서 `SetupPrototypeAssets.Main`을 Unity CLI `command run_script`로 실행하면 재질·임포트 설정·프리팹을 만든다. 기존 프리팹을 덮어쓰므로 수작업 수정이 있다면 먼저 보존한다.
3. `BuildPrototypeShelter.Main`은 적용 전 Main을 위한 1회 배치 도구다. 적용된 씬에서는 중복 방지를 위해 중단한다. 현재 Main에는 실행하지 않는다.
4. Blender에서 `render_prototype_player.py`를 실행하면 스튜디오 렌더를 다시 만든다.

씬/프리팹/재질 수정은 Unity Editor API로 수행하며 YAML을 직접 편집하지 않는다. 재배치 후 NavMesh를 다시 굽고 네 방향 경로와 실제 적 이동을 확인한다.

## 검증과 한계

Unity Play 자동화에서 세 애니메이션 선택·관절 움직임, 네 출입구 CharacterController 통과, 네 스폰 지점 NavMesh 경로와 실제 적 진입을 확인했다. Main↔Collection에서 플레이어/Animator 유지, 생존자 3명의 새 시설 배치, 새 연구소 상호작용, 채집한 목재 46에서 나무 벽 건설 후 26으로 차감되는 흐름도 통과했다. 검증 중 Error/Exception 0, 최종 컴파일 오류 없음이다.

키보드·마우스를 직접 조작한 체감 검사와 배포 빌드는 포함하지 않는다. 이동 애니메이션은 속도 기반 1차 버전이라 옆걸음·뒷걸음·점프 전용 동작, 정밀 조준 IK, 사격/재장전 동작은 없다. 소총 외형이 실제 발사 방향에 맞춰 움직이는 작업도 후속 범위다. 사실적인 최종 캐릭터나 완성된 도시 배경 수준의 텍스처 작업은 아직 하지 않았다.

맵 확장으로 이동 시간·방어선·적 도착 시간이 변했다. 이전 24m 쉘터의 첫날 전투 측정치를 새 배치에 그대로 적용하지 않는다. 적 수·공격력·자원 비용은 이번 작업에서 변경하지 않았다.
