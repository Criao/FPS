using UnityEditor;
using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class AssetBundleBuilder : EditorWindow
{
    [MenuItem("Tools/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        string outputPath = Path.Combine(Application.dataPath, "../Server/public/updates/bundles");

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64
        );

        RenameAssetBundles(outputPath);
        GenerateManifest(outputPath);

        Debug.Log($"AssetBundles built to: {outputPath}");
    }

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
