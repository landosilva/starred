namespace Kynesis.Starred.Editor
{
    using System;
    using System.IO;
    using UnityEngine;

    internal static class EditorJsonFile
    {
        public static bool TryLoad<T>(string filePath, out T data) where T : class
        {
            data = null;
            try
            {
                string fileText = File.ReadAllText(filePath);
                data = JsonUtility.FromJson<T>(fileText);
                return data != null;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception exception)
            {
                StarredLog.Warning($"Failed to read {filePath}: {exception.Message}");
                return false;
            }
        }

        public static void Save<T>(string filePath, T data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception exception)
            {
                StarredLog.Warning($"Failed to save {filePath}: {exception.Message}");
            }
        }
    }
}
