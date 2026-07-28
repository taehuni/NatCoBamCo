using UnityEngine;

// 建筑手动攻击点配置：挂在建筑 prefab 上，用来告诉敌人“可以站在哪里攻击这个建筑”。
// 건물 수동 공격 지점 설정: 건물 프리팹에 붙여서 적에게 "어디에 서서 이 건물을 공격할 수 있는지" 알려준다.
// 如果这个数组有内容，敌人会优先使用这些手动点；如果没有，代码会自动根据 Bounds 生成候选点。
// 이 배열에 값이 있으면 적은 이 수동 지점을 우선 사용한다. 없으면 코드가 Bounds 기준으로 후보 지점을 자동 생성한다.

public class BuildingAttackPoints : MonoBehaviour
{
    public Transform[] attackPoints; // 手动指定的建筑攻击点 / 수동으로 지정한 건물 공격 위치
}
