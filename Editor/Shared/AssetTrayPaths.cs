namespace Kynesis.Starred.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    internal static class AssetTrayPaths
    {
        public static T Find<T>(string fileNameWithExtension) where T : Object
        {
            string baseName = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            string[] guids = AssetDatabase.FindAssets($"{baseName} t:{typeof(T).Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(fileNameWithExtension))
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }
    }
}
