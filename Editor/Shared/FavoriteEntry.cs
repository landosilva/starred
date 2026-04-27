namespace Kynesis.Starred.Editor
{
    using System;

    /// <summary>
    /// A single favorite entry. Either a project asset (identified by GUID) or
    /// a scene / prefab-stage GameObject (identified by Unity's GlobalObjectId
    /// — stable across rename and reparent).
    ///
    /// <see cref="ScenePath"/> and <see cref="HierarchyPath"/> are kept for
    /// display purposes (showing the object's original location) and as a
    /// best-effort fallback if the GlobalObjectId can't be resolved.
    /// </summary>
    [Serializable]
    internal class FavoriteEntry
    {
        public string Guid;
        public string GlobalObjectId;
        public string ScenePath;
        public string HierarchyPath;

        public bool IsSceneObject => !string.IsNullOrEmpty(GlobalObjectId);
        public bool IsAsset       => !IsSceneObject && !string.IsNullOrEmpty(Guid);

        public string LookupKey => IsSceneObject ? $"s:{GlobalObjectId}" : $"a:{Guid}";

        public static FavoriteEntry ForAsset(string guid) => new() { Guid = guid };

        public static FavoriteEntry ForSceneObject(string globalObjectId, string scenePath, string hierarchyPath) =>
            new()
            {
                GlobalObjectId = globalObjectId,
                ScenePath = scenePath,
                HierarchyPath = hierarchyPath,
            };
    }
}
