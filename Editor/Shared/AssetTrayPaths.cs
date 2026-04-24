namespace Kynesis.Starred.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Resolves UXML / USS asset references by file name. Survives the tool
    /// being relocated inside the project or extracted to a UPM package —
    /// avoids the fragility of hard-coded <c>"Assets/_Project/..."</c> paths.
    /// </summary>
    internal static class AssetTrayPaths
    {
        public static T Find<T>(string fileNameWithExtension) where T : Object
        {
            var baseName = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            var guids = AssetDatabase.FindAssets($"{baseName} t:{typeof(T).Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(fileNameWithExtension))
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }
    }
}
