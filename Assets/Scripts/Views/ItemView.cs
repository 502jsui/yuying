using UnityEngine;

public class ItemView : MonoBehaviour
{
    [Header("身份")]
    public string itemId;
    public ItemOwner owner;

    [Header("引用")]
    public SpriteRenderer spriteRenderer;
    public GameObject temperatureGlow;

    [HideInInspector] public ItemData data;

    public void RefreshVisual()
    {
        if (data == null) return;
        if (temperatureGlow) temperatureGlow.SetActive(data.hasTemperature);
    }
}