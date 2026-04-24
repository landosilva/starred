namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEditor;

    /// <summary>
    /// Fires <see cref="Changed"/> after any asset import / delete / move, once
    /// the AssetDatabase has settled. Preferred over <c>EditorApplication.projectChanged</c>
    /// for UI that needs to re-resolve GUIDs, because that event can fire before
    /// the database is updated.
    /// </summary>
    internal sealed class AssetChangeNotifier : AssetPostprocessor
    {
        public static event Action Changed;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0 &&
                deletedAssets.Length == 0 &&
                movedAssets.Length == 0) return;

            Changed?.Invoke();
        }
    }
}
