namespace Kynesis.Starred.Editor
{
    using System;

    /// <summary>
    /// A single favorite entry. Either a project asset (identified by GUID) or a
    /// scene / prefab-stage GameObject (identified by scene path + hierarchy path).
    /// </summary>
    [Serializable]
    internal class FavoriteEntry
    {
        public string Guid;
        public string ScenePath;
        public string HierarchyPath;

        public bool IsSceneObject => !string.IsNullOrEmpty(ScenePath);
        public bool IsAsset       => !IsSceneObject && !string.IsNullOrEmpty(Guid);

        public string LookupKey => IsSceneObject ? $"s:{ScenePath}::{HierarchyPath}" : $"a:{Guid}";

        public static FavoriteEntry ForAsset(string guid) => new() { Guid = guid };

        public static FavoriteEntry ForSceneObject(string scenePath, string hierarchyPath) =>
            new() { ScenePath = scenePath, HierarchyPath = hierarchyPath };
    }
}
