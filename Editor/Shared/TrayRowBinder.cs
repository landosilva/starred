namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal sealed class TrayRowBinding
    {
        public UnityEngine.Object SelectTarget;
        public Action OnDoubleClick;
    }

    internal static class TrayRowBinder
    {
        public static TrayRowBinding BindFor(FavoriteEntry entry, bool useHistorySelection)
        {
            if (entry.IsAsset)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
                UnityEngine.Object asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) return null;
                return new TrayRowBinding
                {
                    SelectTarget = asset,
                    OnDoubleClick = () => AssetDatabase.OpenAsset(asset),
                };
            }

            if (entry.IsSceneObject)
            {
                GameObject gameObject = SceneObjectResolver.Find(entry);
                if (gameObject == null) return null;
                return new TrayRowBinding
                {
                    SelectTarget = gameObject,
                    OnDoubleClick = () =>
                    {
                        if (useHistorySelection) SelectionHistoryTracker.SelectWithoutRecording(gameObject);
                        else Selection.activeGameObject = gameObject;
                        SceneView.FrameLastActiveSceneView();
                    },
                };
            }

            return null;
        }

        public static VisualElement FindRowAncestor(VisualElement element)
        {
            while (element != null && element.userData is not FavoriteEntry)
                element = element.parent;
            return element;
        }

        public static void ApplyEntryHighlight(VisualElement list)
        {
            string selectedGuid = AssetTrayRow.GetCurrentSelectionGuid();
            GameObject selectedGo = Selection.activeGameObject;
            string selectedGoId = selectedGo != null ? SceneObjectResolver.GetGlobalObjectId(selectedGo) : null;

            AssetTrayRow.ApplyCurrentHighlight(list, data =>
            {
                if (data is not FavoriteEntry entry) return false;
                if (entry.IsAsset) return entry.Guid == selectedGuid;
                if (entry.IsSceneObject && !string.IsNullOrEmpty(selectedGoId))
                    return entry.GlobalObjectId == selectedGoId;
                return false;
            });
        }
    }
}
