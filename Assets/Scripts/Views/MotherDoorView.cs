using UnityEngine;

/// <summary>
/// 二楼母亲门的表现：母亲常驻门内，ResidentView 不可见，
/// 所以由这个组件单独读取母亲数据，驱动门缝光与门叶开合。
/// </summary>
[DefaultExecutionOrder(100)]
public class MotherDoorView : MonoBehaviour
{
    public DoorLightView doorLight;

    private void Update()
    {
        if (doorLight == null) return;

        GameRoot root = GameRoot.Instance;
        if (root == null || root.Session == null) return;

        ResidentData mother = root.Session.GetResident(ResidentId.Mother);
        if (mother == null) return;

        doorLight.Apply(mother.warmth, mother.awareness, mother.hasAwareness);
    }
}
