using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.HotUpdate
{
    /// <summary>
    /// 资源清单 - 描述所有可热更新的资源
    /// </summary>
    [Serializable]
    public class AssetManifest
    {
        public string version;
        public int buildNumber;
        public List<AssetBundleInfo> bundles = new List<AssetBundleInfo>();

        public long GetTotalSize()
        {
            long total = 0;
            foreach (var bundle in bundles)
            {
                total += bundle.size;
            }
            return total;
        }
    }

    /// <summary>
    /// AssetBundle 信息
    /// </summary>
    [Serializable]
    public class AssetBundleInfo
    {
        public string name;           // bundle名称 (例如: "scenes/fps.bundle")
        public string hash;           // MD5哈希值
        public long size;             // 文件大小(字节)
        public int priority;          // 下载优先级 (数字越大越优先)
        public List<string> dependencies = new List<string>(); // 依赖的其他bundle
    }
}
