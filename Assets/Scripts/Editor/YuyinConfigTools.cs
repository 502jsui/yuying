using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成 / 校验全局配置资产，让策划有一个可以点的入口。
/// </summary>
public static class YuyinConfigTools
{
    private const string ResourceFolder = "Assets/Resources";
    private const string ConfigPath = ResourceFolder + "/GameConfig.asset";

    [MenuItem("Tools/余音/创建或选中默认配置")]
    public static void CreateOrSelectConfig()
    {
        GameConfig config = EnsureConfigAsset();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    /// <summary>批处理入口：unity -executeMethod YuyinConfigTools.EnsureConfigAssetFromCommandLine</summary>
    public static void EnsureConfigAssetFromCommandLine()
    {
        EnsureConfigAsset();
        AssetDatabase.SaveAssets();
        Debug.Log("GameConfig 资产已就绪：" + ConfigPath);
    }

    public static GameConfig EnsureConfigAsset()
    {
        GameConfig existing = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (existing != null)
        {
            existing.EnsureDefaults();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(ResourceFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        GameConfig created = ScriptableObject.CreateInstance<GameConfig>();
        created.EnsureDefaults();
        AssetDatabase.CreateAsset(created, ConfigPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已创建默认 GameConfig：" + ConfigPath);
        return created;
    }
}
