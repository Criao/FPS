using System;
using System.Collections.Generic;

namespace FPSGame.HotUpdate
{
    /// <summary>
    /// Describes the hot-update bundle catalog published by the server.
    /// </summary>
    [Serializable]
    public class AssetManifest
    {
        public string version;
        public int buildNumber;
        public bool forceUpdate;
        public string updateDescription;
        public List<AssetBundleInfo> bundles = new List<AssetBundleInfo>();
    }

    /// <summary>
    /// One downloadable AssetBundle entry.
    /// </summary>
    [Serializable]
    public class AssetBundleInfo
    {
        public string name;
        public string hash;
        public long size;
    }
}
