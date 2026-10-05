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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitData();
    }

    private void InitData()
    {
        residents = new ResidentData[residentViews.Length];
        for (int i = 0; i < residentViews.Length; i++)
        {
            ResidentView view = residentViews[i];
            ResidentData data = CreateResidentData(view.id);
            residents[i] = data;
            view.data = data;
            view.RefreshVisual();
        }

        items = new ItemData[itemViews.Length];
        for (int i = 0; i < itemViews.Length; i++)
        {
            ItemView view = itemViews[i];
            ItemData data = new ItemData(view.itemId, view.owner);
            items[i] = data;
            view.data = data;
        }

        Debug.Log($"初始化 {residents.Length} 个住户，{items.Length} 件物品");
    }

    private ResidentData CreateResidentData(ResidentId id)
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
