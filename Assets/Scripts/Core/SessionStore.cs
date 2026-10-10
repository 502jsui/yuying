using System.IO;
using UnityEngine;

/// <summary>
/// 调试用的一局快照存取（JSON）。Demo 不需要正式存档，
/// 但有了它，出问题时可以把一局存下来复现。
/// </summary>
public static class SessionStore
{
    public static string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, "yuyin_session.json"); }
    }

    public static bool HasSave
    {
        get { return File.Exists(SavePath); }
    }

    public static bool Save(GameSession session)
    {
        if (session == null) return false;
        try
        {
            string json = JsonUtility.ToJson(session.CreateSnapshot(), true);
            File.WriteAllText(SavePath, json);
            Debug.Log("[SessionStore] 已保存快照：" + SavePath);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public static bool Load(GameSession session)
    {
        if (session == null || !HasSave) return false;
        try
        {
            string json = File.ReadAllText(SavePath);
            GameSessionSnapshot snapshot = JsonUtility.FromJson<GameSessionSnapshot>(json);
            session.LoadSnapshot(snapshot);
            Debug.Log("[SessionStore] 已读取快照：" + SavePath);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public static void Delete()
    {
        if (HasSave) File.Delete(SavePath);
    }
}
