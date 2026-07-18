using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HotUpdateWeaponPickupAutoBuild
{
    private const int RequiredBuildNumber = 6;
    private const string MainManifestName = "bundles";
    private const string RequiredBundleName = "submachine_gun.unity3d";
    private const string RequiredManifestBundleName = "submachine_gun";

    static HotUpdateWeaponPickupAutoBuild()
    {
        EditorApplication.delayCall -= BuildIfNeeded;
        EditorApplication.delayCall += BuildIfNeeded;
    }

    private static void BuildIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfNeeded;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || !NeedsBuild())
        {
            return;
        }

        AssetDatabase.Refresh();
        AssetBundleBuilder.BuildAllAssetBundles();
        AssetDatabase.Refresh();
        Debug.Log("Hot update weapon pickup bundle is ready.");
    }

    private static bool NeedsBuild()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string bundleDirectory = Path.Combine(projectRoot, "Server/public/updates/bundles");
        string bundlePath = Path.Combine(bundleDirectory, RequiredBundleName);
        string bundleManifestPath = Path.Combine(bundleDirectory, MainManifestName + ".manifest");
        string manifestPath = Path.Combine(projectRoot, "Server/public/updates/manifest.json");

        if (!File.Exists(bundlePath) ||
            !File.Exists(bundleManifestPath) ||
            !File.Exists(manifestPath))
        {
            return true;
        }

        string manifest = File.ReadAllText(manifestPath);
        if (!manifest.Contains($"\"buildNumber\": {RequiredBuildNumber}") ||
            !manifest.Contains($"\"name\": \"{RequiredBundleName}\""))
        {
            return true;
        }

        string bundleManifest = File.ReadAllText(bundleManifestPath);
        return !bundleManifest.Contains($"Name: {RequiredManifestBundleName}");
    }
}
