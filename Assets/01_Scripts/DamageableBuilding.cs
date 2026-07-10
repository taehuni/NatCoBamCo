using UnityEngine;

public class DamageableBuilding : MonoBehaviour
{
    public float hp = 100f;
    public float maxHp = 100f;
    public int defensePower = 0;
    //public float repairTime;  수리 시간

    public void GetDamage(float damage)
    {
        float finalDamage = damage - defensePower;

        if (finalDamage < 5)
        {
            finalDamage = 5;
        }

        hp -= finalDamage;

        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }

    //기본 수리 함수
    public void Repair(float healAmount)
    {
        if (hp == maxHp)
        {
            return;
        }
        else
        {
            hp += healAmount;
            if (hp > maxHp)
            {
                hp = maxHp;
            }
        }

        //자원 소모 추가해야 함
        //수리 진도는 Silder으로 표현

    }
}