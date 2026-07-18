using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AssetBundleBuilder
{
    private const string ManifestVersion = "1.1.4";
    private const int ManifestBuildNumber = 6;
    private const bool ManifestForceUpdate = false;
    private const string ManifestUpdateDescription = "Add hot-update submachine gun pickup";

    [MenuItem("Tools/Build AssetBundles")]
    [MenuItem("FPS Game/Build Hot Update Bundles")]
    public static void BuildAllAssetBundles()
    {
        string outputPath = Path.Combine(Application.dataPath, "../Server/public/updates/bundles");

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        CleanOutputDirectory(outputPath);

        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        RenameAssetBundles(outputPath);
        GenerateManifest(outputPath);

        Debug.Log($"AssetBundles built to: {outputPath}");
    }

    private static void CleanOutputDirectory(string outputPath)
    {
        DirectoryInfo directory = new DirectoryInfo(outputPath);
        foreach (FileInfo file in directory.GetFiles())
        {
            file.Delete();
        }
    }

    private static void RenameAssetBundles(string bundlePath)
    {
        DirectoryInfo directory = new DirectoryInfo(bundlePath);

        foreach (FileInfo file in directory.GetFiles())
        {
            if (!string.IsNullOrEmpty(file.Extension) ||
                file.Name == "bundles" ||
                file.Name.EndsWith(".manifest"))
            {
                continue;
            }

            string newName = file.FullName + ".unity3d";
            if (File.Exists(newName))
            {
                File.Delete(newName);
            }

            file.MoveTo(newName);
            Debug.Log($"Renamed: {file.Name} -> {file.Name}.unity3d");
        }
    }

    private static void GenerateManifest(string bundlePath)
    {
        StringBuilder manifestJson = new StringBuilder();
        manifestJson.AppendLine("{");
        manifestJson.AppendLine($"  \"version\": \"{ManifestVersion}\",");
        manifestJson.AppendLine($"  \"buildNumber\": {ManifestBuildNumber},");
        manifestJson.AppendLine($"  \"forceUpdate\": {ManifestForceUpdate.ToString().ToLowerInvariant()},");
        manifestJson.AppendLine($"  \"updateDescription\": \"{ManifestUpdateDescription}\",");
        manifestJson.AppendLine("  \"bundles\": [");

        List<FileInfo> files = GetManifestFiles(bundlePath);
        for (int i = 0; i < files.Count; i++)
        {
            FileInfo file = files[i];
            string hash = CalculateMD5(file.FullName);

            manifestJson.AppendLine("    {");
            manifestJson.AppendLine($"      \"name\": \"{file.Name}\",");
            manifestJson.AppendLine($"      \"hash\": \"{hash}\",");
            manifestJson.AppendLine($"      \"size\": {file.Length}");
            manifestJson.Append("    }");
            manifestJson.AppendLine(i < files.Count - 1 ? "," : string.Empty);
        }

        manifestJson.AppendLine("  ]");
        manifestJson.AppendLine("}");

        string manifestPath = Path.Combine(bundlePath, "../manifest.json");
        File.WriteAllText(manifestPath, manifestJson.ToString());

        Debug.Log($"Manifest generated: {manifestPath}");
    }

    private static List<FileInfo> GetManifestFiles(string bundlePath)
    {
        DirectoryInfo directory = new DirectoryInfo(bundlePath);
        List<FileInfo> files = new List<FileInfo>();

        FileInfo mainManifestBundle = new FileInfo(Path.Combine(bundlePath, "bundles"));
        if (mainManifestBundle.Exists)
        {
            files.Add(mainManifestBundle);
        }

        foreach (FileInfo bundleFile in directory.GetFiles("*.unity3d"))
        {
            files.Add(bundleFile);
        }

        return files;
    }

    private static string CalculateMD5(string filePath)
    {
        using (MD5 md5 = MD5.Create())
        using (FileStream stream = File.OpenRead(filePath))
        {
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
