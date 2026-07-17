using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("적군 설정")]
    public float maxHp = 30;
    public float health = 30;
    public float moveSpeed = 3f;
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;

    public float defensePower = 10; //현재의 방어력
    public float defenseConstant = 100f;
    private NavMeshAgent agent;
    public enum EnemyGrade //적의 등급 유형 Enum
    {
        Normal, //일반
        Elite, //엘리트
        Boss
    }
    public EnemyGrade enemyGrade; //적의 속성 유형

    public enum EnemyClass
    {
        Standard, //기본
        Fast, //고속
        Tank, //탱크
        Ranged //원거리
    }
    public EnemyClass enemyClass;
    public float detectRange; //우성 공격 대상 감지 범위



    [Header("전기 타워 관한 변수")]
    public float curParalyzeStack; //현재 마비 stack 수
    public float maxParalyzeStack = 100; //마비상태 들어가려면 필요한 미비 stack 수
    public float paralyzeDefensePower = 0.1f; //보스,엘리트 적 미비 저항력(0 ~ 1)
    private float paralyzeEndTime; //마비 상태 끝나는 시간
    private bool isParalyzed; //마비 상태인지 여부
    private bool isSlowed; //감속 상태인지 여부
    private float slowEndTime; //감속 상태 끝나는 시간
    private float currentSlowPower; //감속 상태에서 현재 감속력
    private bool isDefenseReduced; //방어력 감소 상태
    private float defenseReductionEndTime; //방어력 감소 종료 시간
    private float originalDefensePower; //원래 방어력
    private float currentDefenseReduceAmount;

    // [Header("다른 타워 % 방어력 감소할 때 사용")]
    // private bool isDamageReductionReduced;
    // private float damageReductionReduceAmount;
    // private float damageReductionReduceEndTime;


    void Start()
    {
        health = maxHp;
        agent = GetComponent<NavMeshAgent>();
        originalDefensePower = defensePower;


    }

    void Update()
    {
        if (HandleParalyzeState())
        {
            return; //마비 상태면 이동, 공격 못함
        }

        HandleSlowState(); //감속 상태 처리
        HandleDefenseReductionState();
    }

    //감속 상태 처리
    void HandleSlowState()
    {
        if (!isSlowed)
        {
            return;
        }

        if (Time.time >= slowEndTime)
        {
            isSlowed = false;
            currentSlowPower = 0f; //감속력 초기화

            ResetSpeed();
        }
    }

    void HandleDefenseReductionState()
    {
        if (!isDefenseReduced)
        {
            return;
        }

        if (Time.time >= defenseReductionEndTime)
        {
            isDefenseReduced = false;
            currentDefenseReduceAmount = 0;
            defensePower = originalDefensePower;
        }
    }

    //마비 상태 판단
    bool HandleParalyzeState()
    {
        if (!isParalyzed)
        {
            return false;
        }

        if (Time.time >= paralyzeEndTime)
        {
            isParalyzed = false;

            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;
            }

            return false;
        }

        return true;
    }

    //마비상태 return
    public bool IsParalyzed()
    {
        return isParalyzed;
    }


    //마비 상태 stack 증가
    public void AddParalyzeStack(float amount, float duration)
    {
        if (isParalyzed)
        {
            return;
        }

        curParalyzeStack += amount * (1f - paralyzeDefensePower);

        if (curParalyzeStack >= maxParalyzeStack)
        {
            //Debug.Log(gameObject.name + " 가 마비상태 들어가다");
            curParalyzeStack = maxParalyzeStack;
            //마비상태 들어가기
            EnterParalyze(duration);
        }
    }

    //마비 상태 들어가기
    void EnterParalyze(float duration)
    {
        isParalyzed = true;
        curParalyzeStack = 0;
        paralyzeEndTime = Time.time + duration;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
    }

    //다매지 받기
    public void TakeDamage(float defaultDamage, float ignoreDefenseRate)
    {
        //根据防御力计算减防比例
        float damageReduction = GetCurrentDamageReduction();

        damageReduction -= ignoreDefenseRate; //방어 무시 비율 无视防御比例
        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f); //min 0, max 0.9

        //最终伤害计算
        float finalDamage = defaultDamage * (1f - damageReduction);
        finalDamage = Mathf.Max(finalDamage, 1f);
        finalDamage = RoundToTwoDecimals(finalDamage);
        Debug.Log("FinalDamage = " + finalDamage);
        health -= finalDamage;
        if (health <= 0) Dead();
    }

    public void TakeDamage(float defaultDamage)
    {
        TakeDamage(defaultDamage, 0f);
    }

    //방어 비례 계산 함수
    public float GetCurrentDamageReduction()
    {
        float damageReduction = defensePower / (defensePower + defenseConstant);

        // 以后如果有真正的百分比减防 debuff，就放这里
        // if (isDamageReductionReduced)
        // {
        //     damageReduction -= damageReductionReduceAmount;
        // }

        damageReduction = Mathf.Clamp(damageReduction, 0f, 0.9f);

        return damageReduction;
    }

    //소수점 뒤에 2자리 저장
    float RoundToTwoDecimals(float value)
    {
        return Mathf.Round(value * 100f) / 100f;
    }

    //속도 감소
    public void SetSpeedDown(float coldPower, float coldTime)
    {
        if (agent == null || !agent.enabled)
        {
            return;
        }

        isSlowed = true;

        if (coldPower >= currentSlowPower) //새로운 감속력이 현재 감속력보다 크면
        {
            currentSlowPower = coldPower; //현재 감속력 업데이트
            agent.speed = moveSpeed * (1f - currentSlowPower); //감속력 적용
            //Debug.Log(gameObject.name + "가 감속 상태 들어가다");
            slowEndTime = Time.time + coldTime; //감속 상태 끝나는 시간 업데이트
        }
    }

    //DefensePower 감소 
    public void ReduceDefensePower(float amount, float duration)
    {

        if (amount < currentDefenseReduceAmount)
        {
            return;
        }

        currentDefenseReduceAmount = amount;
        isDefenseReduced = true;
        defenseReductionEndTime = Time.time + duration;

        defensePower = originalDefensePower * (1f - amount);
    }

    //속도 회복
    void ResetSpeed()
    {
        if (agent != null && agent.enabled)
        {
            agent.speed = moveSpeed;
        }
    }

    public void Dead()
    {
        Destroy(gameObject);
        //나중에 사망 animation 추가
    }
}