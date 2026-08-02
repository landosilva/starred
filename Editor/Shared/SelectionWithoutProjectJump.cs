namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    internal static class SelectionWithoutProjectJump
    {
        private static bool _reflectionReady;
        private static Type _projectBrowserType;
        private static PropertyInfo _isLockedProperty;

        public static void Select(UnityEngine.Object target)
        {
            if (target == null) return;

            if (!EditorUtility.IsPersistent(target))
            {
                Selection.activeObject = target;
                return;
            }

            EnsureReflection();
            if (_projectBrowserType == null || _isLockedProperty == null)
            {
                Selection.activeObject = target;
                return;
            }

            UnityEngine.Object[] browsers = Resources.FindObjectsOfTypeAll(_projectBrowserType);
            if (browsers == null || browsers.Length == 0)
            {
                Selection.activeObject = target;
                return;
            }

            List<(UnityEngine.Object browser, bool wasLocked)> previousLocks = new List<(UnityEngine.Object browser, bool wasLocked)>(browsers.Length);
            foreach (UnityEngine.Object browser in browsers)
            {
                bool wasLocked = (bool)_isLockedProperty.GetValue(browser);
                previousLocks.Add((browser, wasLocked));
                if (!wasLocked)
                    _isLockedProperty.SetValue(browser, true);
            }

            Selection.activeObject = target;

            EditorApplication.delayCall += () =>
            {
                foreach ((UnityEngine.Object browser, bool wasLocked) in previousLocks)
                {
                    if (browser == null) continue;
                    _isLockedProperty.SetValue(browser, wasLocked);
                }
            };
        }

        private static void EnsureReflection()
        {
            if (_reflectionReady) return;
            _reflectionReady = true;

            _projectBrowserType =
                Type.GetType("UnityEditor.ProjectBrowser,UnityEditor")
                ?? Type.GetType("UnityEditor.ProjectBrowser,UnityEditor.CoreModule")
                ?? FindTypeInLoadedAssemblies("UnityEditor.ProjectBrowser");

            _isLockedProperty = _projectBrowserType?.GetProperty(
                "isLocked",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type FindTypeInLoadedAssemblies(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName); }
                catch (ReflectionTypeLoadException) { continue; }
                if (type != null) return type;
            }
            return null;
        }
    }
}
