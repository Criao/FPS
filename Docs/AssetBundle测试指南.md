# AssetBundle 测试指南

## 1. 在 Unity 中构建
- 打开 Tools > AssetBundle Builder
- 设置 Version: 1.1.0
- 设置 Build Number: 2
- 点击 "Build AssetBundles"

## 2. 检查输出
构建完成后会打开文件夹，检查：
- AssetBundles/StandaloneWindows64/manifest.json (资源清单)
- AssetBundles/StandaloneWindows64/test/testprefab (你的 bundle 文件)
- AssetBundles/StandaloneWindows64/StandaloneWindows64 (主清单文件)

## 3. 部署到服务器
复制文件到服务器目录：
```bash
# Windows PowerShell
Copy-Item "AssetBundles\StandaloneWindows64\*" -Destination "e:\unity project\FPS\Server\public\updates\bundles\" -Recurse -Force
Copy-Item "AssetBundles\StandaloneWindows64\manifest.json" -Destination "e:\unity project\FPS\Server\public\updates\" -Force
```

## 4. 测试下载
- 确保服务器正在运行
- 确保本地 AppVersion.json 的 buildNumber 是 1
- 运行 Login 场景
- 观察控制台日志

## 5. 测试加载
创建测试脚本加载 AssetBundle：
```csharp
StartCoroutine(AssetBundleManager.Instance.LoadAsset<GameObject>(
    "test/testprefab", 
    "TestPrefab", 
    (prefab) => {
        if (prefab != null) {
            Instantiate(prefab);
            Debug.Log("AssetBundle loaded successfully!");
        }
    }
));
```

## 预期结果
✓ 检测到更新
✓ 下载进度显示
✓ 文件保存到 persistentDataPath/AssetBundles/
✓ MD5 校验通过
✓ 版本号更新为 2
