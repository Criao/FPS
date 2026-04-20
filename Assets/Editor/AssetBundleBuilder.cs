using UnityEditor;
using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// AssetBundle 构建工具
/// 用于在编辑器中构建、重命名和生成资源清单
/// </summary>
public class AssetBundleBuilder : EditorWindow
{
    /// <summary>
    /// 构建所有 AssetBundles
    /// 菜单路径: Tools/Build AssetBundles
    /// </summary>
    [MenuItem("Tools/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        string outputPath = Path.Combine(Application.dataPath, "../Server/public/updates/bundles");

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // 构建 AssetBundles
        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64
        );

        // 重命名文件（添加 .unity3d 扩展名）
        RenameAssetBundles(outputPath);

        // 生成资源清单文件
        GenerateManifest(outputPath);

        Debug.Log($"AssetBundles built to: {outputPath}");
    }

    /// <summary>
    /// 重命名 AssetBundle 文件，添加 .unity3d 扩展名
    /// </summary>
    static void RenameAssetBundles(string bundlePath)
    {
        DirectoryInfo dir = new DirectoryInfo(bundlePath);
        FileInfo[] files = dir.GetFiles();

        foreach (FileInfo file in files)
        {
            // 跳过已有扩展名的文件
            if (file.Extension != "" || file.Name == "bundles" || file.Name.EndsWith(".manifest"))
                continue;

            string newName = file.FullName + ".unity3d";
            if (!File.Exists(newName))
            {
                file.MoveTo(newName);
                Debug.Log($"Renamed: {file.Name} -> {file.Name}.unity3d");
            }
        }
    }

    /// <summary>
    /// 生成资源清单 JSON 文件
    /// 包含版本号、构建号和所有 bundle 的信息（名称、哈希、大小）
    /// </summary>
    static void GenerateManifest(string bundlePath)
    {
        StringBuilder manifestJson = new StringBuilder();
        manifestJson.AppendLine("{");
        manifestJson.AppendLine("  \"version\": \"1.1.0\",");
        manifestJson.AppendLine("  \"buildNumber\": 2,");
        manifestJson.AppendLine("  \"bundles\": [");

        DirectoryInfo dir = new DirectoryInfo(bundlePath);
        FileInfo[] files = dir.GetFiles("*.unity3d");

        for (int i = 0; i < files.Length; i++)
        {
            FileInfo file = files[i];
            string hash = CalculateMD5(file.FullName);

            manifestJson.AppendLine("    {");
            manifestJson.AppendLine($"      \"name\": \"{file.Name}\",");
            manifestJson.AppendLine($"      \"hash\": \"{hash}\",");
            manifestJson.AppendLine($"      \"size\": {file.Length}");
            manifestJson.Append("    }");

            if (i < files.Length - 1)
                manifestJson.AppendLine(",");
            else
                manifestJson.AppendLine();
        }

        manifestJson.AppendLine("  ]");
        manifestJson.AppendLine("}");

        string manifestPath = Path.Combine(bundlePath, "../manifest.json");
        File.WriteAllText(manifestPath, manifestJson.ToString());

        Debug.Log($"Manifest generated: {manifestPath}");
    }

    /// <summary>
    /// 计算文件的 MD5 哈希值
    /// </summary>
    static string CalculateMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return System.BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}
