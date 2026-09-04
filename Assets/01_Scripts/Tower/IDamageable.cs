// 可受伤能力接口：实现这个接口的对象，表示它能够接收伤害。
// 피해 가능 능력 인터페이스: 이 인터페이스를 구현한 오브젝트는 피해를 받을 수 있다.
public interface IDamageable
{
    // 对对象造成指定数值的原始伤害。
    // 오브젝트에 지정된 수치의 원래 피해를 적용한다.
    void TakeDamage(float damage);
}
