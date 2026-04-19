using UnityEngine;

namespace FPSGame.Utils
{
    /// <summary>
    /// 统一日志管理工具
    /// </summary>
    public static class Logger
    {
        private static bool enableLog = true;

        public static void SetLogEnabled(bool enabled)
        {
            enableLog = enabled;
        }

        public static void Log(string message)
        {
            if (enableLog)
                Debug.Log($"[FPSGame] {message}");
        }

        public static void LogWarning(string message)
        {
            if (enableLog)
                Debug.LogWarning($"[FPSGame] {message}");
        }

        public static void LogError(string message)
        {
            if (enableLog)
                Debug.LogError($"[FPSGame] {message}");
        }
    }
}
