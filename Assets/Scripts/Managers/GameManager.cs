using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("场景引用")]
    public PlayerController player;
    public ResidentView[] residentViews;
    public ItemView[] itemViews;

    [HideInInspector] public ResidentData[] residents;
    [HideInInspector] public ItemData[] items;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitData();
    }

    void InitData()
    {
        residents = new ResidentData[residentViews.Length];
        for (int i = 0; i < residentViews.Length; i++)
        {
            var v = residentViews[i];
            var d = CreateResidentData(v.id);
            residents[i] = d;
            v.data = d;
        }

        items = new ItemData[itemViews.Length];
        for (int i = 0; i < itemViews.Length; i++)
        {
            var v = itemViews[i];
            var d = new ItemData(v.itemId, v.owner);
            items[i] = d;
            v.data = d;
        }

        Debug.Log($"初始化 {residents.Length} 个住户，{items.Length} 件物品");
    }

    ResidentData CreateResidentData(ResidentId id)
    {
        switch (id)
        {
            case ResidentId.Mother: return new ResidentData(id, 30, 30, true);
            case ResidentId.Grandma: return new ResidentData(id, 26, 20, true);
            case ResidentId.Aunt: return new ResidentData(id, 26, 20, true);
            case ResidentId.Girl: return new ResidentData(id, 30, 0, false);
        }
        return null;
    }

    public void OnPlayerTouch(ItemView item)
    {
        Debug.Log($"玩家触碰了 {item.itemId}（owner={item.owner}）");
    }
}