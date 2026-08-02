namespace Kynesis.Starred.Editor
{
    using System;

    [Serializable]
    internal class FavoriteEntry
    {
        public string Guid;
        public string GlobalObjectId;
        public string ScenePath;
        public string HierarchyPath;
        public string DisplayName;

        public bool IsSceneObject => !string.IsNullOrEmpty(GlobalObjectId);
        public bool IsAsset => !IsSceneObject && !string.IsNullOrEmpty(Guid);

        public string LookupKey => IsSceneObject ? $"s:{GlobalObjectId}" : $"a:{Guid}";

        public static FavoriteEntry ForAsset(string guid) => new() { Guid = guid };

        public static FavoriteEntry ForSceneObject(string globalObjectId, string scenePath, string hierarchyPath) =>
            new()
            {
                GlobalObjectId = globalObjectId,
                ScenePath = scenePath,
                HierarchyPath = hierarchyPath,
                DisplayName = LastSegment(hierarchyPath),
            };

        public static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
