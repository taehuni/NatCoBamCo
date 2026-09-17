using UnityEngine;

// 테스트용 간이 낮/밤 타이머. 실제 낮/밤 시스템이 완성되면 이 스크립트는 교체될 예정.
// dayDuration/nightDuration 이 지나면 전환되면서, 씬에 있는 정비공 행동/생존자 배회 컴포넌트에 SetNight() 를 뿌려준다.
public class DayNightCycleTest : MonoBehaviour
{
    [Header("낮/밤 길이 (초)")]
    public float dayDuration = 60f;
    public float nightDuration = 30f;

    [Header("상태 (읽기 전용)")]
    public bool isNight;

    private float timer;

    void Start()
    {
        timer = dayDuration;
        Broadcast();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            isNight = !isNight;
            timer = isNight ? nightDuration : dayDuration;
            Broadcast();

            Debug.Log(isNight ? "밤이 되었습니다" : "낮이 되었습니다");
        }
    }

    void Broadcast()
    {
        foreach (SurvivorMechanicBehaviour mechanic in FindObjectsOfType<SurvivorMechanicBehaviour>())
        {
            mechanic.SetNight(isNight);
        }

        foreach (SurvivorFreeMove roamer in FindObjectsOfType<SurvivorFreeMove>())
        {
            roamer.SetNight(isNight);
        }
    }
}
