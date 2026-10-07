using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 极简全局事件总线。逻辑系统之间通过事件解耦：
/// 数值系统不需要认识 UI，物品系统不需要认识门光。
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

    /// <summary>订阅类型为 T 的事件。同一个方法重复订阅会被忽略。</summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        if (handler == null) return;
        Type type = typeof(T);
        Delegate existing;
        if (handlers.TryGetValue(type, out existing))
        {
            Delegate combined = Delegate.Combine(existing, handler);
            handlers[type] = combined;
        }
        else
        {
            handlers[type] = handler;
        }
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;
        Type type = typeof(T);
        Delegate existing;
        if (!handlers.TryGetValue(type, out existing)) return;

        Delegate remaining = Delegate.Remove(existing, handler);
        if (remaining == null) handlers.Remove(type);
        else handlers[type] = remaining;
    }

    /// <summary>发布事件。发布时使用快照，允许订阅者在回调里退订。</summary>
    public static void Publish<T>(T evt)
    {
        Type type = typeof(T);
        Delegate existing;
        if (!handlers.TryGetValue(type, out existing)) return;
        if (existing == null) return;

        Delegate[] snapshot = existing.GetInvocationList();
        for (int i = 0; i < snapshot.Length; i++)
        {
            try
            {
                ((Action<T>)snapshot[i]).Invoke(evt);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    /// <summary>是否已经有人订阅该类型事件（调试用）。</summary>
    public static bool HasSubscribers<T>()
    {
        return handlers.ContainsKey(typeof(T));
    }

    public static void Clear()
    {
        handlers.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Clear();
    }
}
