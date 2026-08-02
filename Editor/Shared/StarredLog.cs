namespace Kynesis.Starred.Editor
{
    using UnityEngine;

    internal static class StarredLog
    {
        private const string Prefix = "<color=#FAC846>[Starred]</color>";

        public static void Warning(string message)
        {
            Debug.LogWarning($"{Prefix} {message}");
        }
    }
}
