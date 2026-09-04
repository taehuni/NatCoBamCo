// 可修理能力接口：实现这个接口的对象，表示它能够恢复耐久值。
// 수리 가능 능력 인터페이스: 이 인터페이스를 구현한 오브젝트는 내구도를 회복할 수 있다.
public interface IRepairable
{
    // 为对象恢复指定数值的生命值。
    // 오브젝트의 체력을 지정된 수치만큼 회복한다.
    void Repair(float repairAmount);
}
