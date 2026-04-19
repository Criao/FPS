using System;
using UnityEngine;

namespace FPSGame.Utils
{
    /// <summary>
    /// JSON序列化辅助工具
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 对象转JSON字符串
        /// </summary>
        public static string ToJson(object obj, bool prettyPrint = false)
        {
            try
            {
                return JsonUtility.ToJson(obj, prettyPrint);
            }
            catch (Exception e)
            {
                Logger.LogError($"JSON序列化失败: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// JSON字符串转对象
        /// </summary>
        public static T FromJson<T>(string json)
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Logger.LogError($"JSON反序列化失败: {e.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// 覆盖对象数据（用于更新现有对象）
        /// </summary>
        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
            try
            {
                JsonUtility.FromJsonOverwrite(json, objectToOverwrite);
            }
            catch (Exception e)
            {
                Logger.LogError($"JSON覆盖失败: {e.Message}");
            }
        }
    }
}
