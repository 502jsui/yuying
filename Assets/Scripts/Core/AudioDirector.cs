using UnityEngine;

/// <summary>
/// 音效触发中枢：订阅玩法事件，按事件播放 SfxLibrary 里的音效。
/// 只管"什么事件响什么音"，不产生任何玩法效果。
/// </summary>
[DefaultExecutionOrder(-780)]
public class AudioDirector : MonoBehaviour
{
    private AudioManager audioManager;
    private bool? lastOutdoor;

    private AudioManager Audio
    {
        get
        {
            if (audioManager == null && GameRoot.Instance != null) audioManager = GameRoot.Instance.Audio;
            return audioManager;
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<ItemTouchedEvent>(OnItemTouched);
        EventBus.Subscribe<ItemAppearedEvent>(OnItemAppeared);
        EventBus.Subscribe<ItemTakenEvent>(OnItemTaken);
        EventBus.Subscribe<ItemMovedEvent>(OnItemMoved);
        EventBus.Subscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
        EventBus.Subscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Subscribe<NpcDoorEvent>(OnNpcDoor);
        EventBus.Subscribe<NpcActionEvent>(OnNpcAction);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ItemTouchedEvent>(OnItemTouched);
        EventBus.Unsubscribe<ItemAppearedEvent>(OnItemAppeared);
        EventBus.Unsubscribe<ItemTakenEvent>(OnItemTaken);
        EventBus.Unsubscribe<ItemMovedEvent>(OnItemMoved);
        EventBus.Unsubscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
        EventBus.Unsubscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Unsubscribe<NpcDoorEvent>(OnNpcDoor);
        EventBus.Unsubscribe<NpcActionEvent>(OnNpcAction);
    }

    private void OnItemTouched(ItemTouchedEvent evt)
    {
        Play(SfxId.Touch);
    }

    private void OnItemAppeared(ItemAppearedEvent evt)
    {
        Play(SfxId.ItemPlace);
    }

    private void OnItemTaken(ItemTakenEvent evt)
    {
        Play(SfxId.ItemTake);
    }

    private void OnItemMoved(ItemMovedEvent evt)
    {
        // 小女孩把东西挪走：用"收走"的音色
        Play(SfxId.ItemTake);
    }

    private void OnForceTouchWarned(ForceTouchWarnedEvent evt)
    {
        Play(SfxId.Warn);
    }

    private void OnItemPrompt(ItemPromptEvent evt)
    {
        // 强碰真的生效了：补一个柔和的一下
        if (evt.status == ItemActionStatus.ForceTouchAccepted) Play(SfxId.WarnSoft);
    }

    private void OnNpcDoor(NpcDoorEvent evt)
    {
        Play(evt.opening ? SfxId.DoorOpen : SfxId.DoorClose);
    }

    private void OnNpcAction(NpcActionEvent evt)
    {
        // 楼里有人交流 → 雨声断一拍
        if (evt.executed && evt.action == NpcActionType.Talk)
        {
            AudioManager manager = Audio;
            if (manager != null) manager.TriggerRainPause();
        }
    }

    private void Update()
    {
        // 雨声：进后院淡入、回一楼淡出（不做 BGM）
        AudioManager manager = Audio;
        if (manager == null) return;

        bool outdoor = IsOutdoorArea();
        if (lastOutdoor.HasValue && lastOutdoor.Value == outdoor) return;

        lastOutdoor = outdoor;
        manager.FadeRainTo(outdoor ? 1f : 0f, 1.5f);
    }

    private static bool IsOutdoorArea()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.Session == null) return false;
        return root.Session.playerArea == AreaId.Backyard;
    }

    private void Play(SfxId id)
    {
        AudioManager manager = Audio;
        if (manager != null) manager.PlaySfx(id);
    }
}
