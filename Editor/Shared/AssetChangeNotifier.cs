namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEditor;

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
