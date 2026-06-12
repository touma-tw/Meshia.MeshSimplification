#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Touma.MeshSimplification.Ndmf.Editor
{
    /// <summary>
    /// Replaces the original Meshia components (or any earlier-named build of this fork)
    /// in place with the current Touma-fork components, carrying their settings across.
    ///
    /// Works by serialized-field copy keyed on type *name*, so it needs no compile-time
    /// reference to the old assembly. The old package must still be installed (old
    /// components must be loadable, not "missing script") while migrating.
    ///
    /// Available as Tools-menu actions (selected hierarchy) and as a button that appears
    /// in the Inspector header only while the selected object still holds an old component.
    /// </summary>
    [InitializeOnLoad]
    static class SettingsMigrationTool
    {
        const string Root = "Tools/Mesh Simplification - Touma Fork/";
        const string MigratePath = Root + "Migrate old settings (selected hierarchy)";
        const string CleanupPath = Root + "Remove old components (selected hierarchy)";

        static SettingsMigrationTool()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnHeaderGUI;
        }

        // Type *names* of components produced by older versions / the original Meshia.
        static bool IsOldCascading(string typeName) =>
            typeName is "MeshiaCascadingAvatarMeshSimplifier" or "CascadingAvatarMeshSimplifier";
        static bool IsOldPerRenderer(string typeName) =>
            typeName is "MeshiaMeshSimplifier";
        static bool IsOld(string typeName) => IsOldCascading(typeName) || IsOldPerRenderer(typeName);

        // ---------- Inspector header button (only when an old component is present) ----------

        static void OnHeaderGUI(UnityEditor.Editor editor)
        {
            var targets = editor.targets.OfType<GameObject>().Where(go => go != null && HasOldComponent(go)).ToArray();
            if (targets.Length == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This object uses an older Mesh Simplification component. " +
                "Migrate it to the Touma fork (replaced in place, keeping your settings).",
                MessageType.Info);
            if (GUILayout.Button("Migrate to Touma Fork"))
            {
                // Defer: don't mutate the hierarchy while the inspector header is drawing.
                EditorApplication.delayCall += () =>
                {
                    var report = new StringBuilder();
                    int migrated = 0, strays = 0;
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Migrate mesh-simplification settings");
                    foreach (var go in targets)
                        if (go != null)
                            foreach (var old in go.GetComponents<MonoBehaviour>())
                                if (old != null && IsOld(old.GetType().Name))
                                    migrated += MigrateOne(old, report, ref strays);
                    ShowResult(migrated, strays, report);
                };
            }
        }

        static bool HasOldComponent(GameObject go)
        {
            foreach (var c in go.GetComponents<MonoBehaviour>())
                if (c != null && IsOld(c.GetType().Name)) return true;
            return false;
        }

        // ---------- Tools menu ----------

        [MenuItem(MigratePath, true)]
        [MenuItem(CleanupPath, true)]
        static bool Validate() => Selection.activeGameObject != null;

        [MenuItem(MigratePath, false)]
        static void MigrateHierarchy()
        {
            var root = Selection.activeGameObject;
            if (root == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Migrate mesh-simplification settings");

            var report = new StringBuilder();
            int migrated = 0, strays = 0;
            foreach (var old in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (old != null && IsOld(old.GetType().Name))
                    migrated += MigrateOne(old, report, ref strays);

            ShowResult(migrated, strays, report);
        }

        [MenuItem(CleanupPath, false)]
        static void RemoveOld()
        {
            var root = Selection.activeGameObject;
            if (root == null) return;

            int removed = 0;
            foreach (var old in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (old != null && IsOld(old.GetType().Name))
                {
                    Undo.DestroyObjectImmediate(old);
                    removed++;
                }

            EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                removed == 0 ? "No old components found under the selection." : $"Removed {removed} old component(s).", "OK");
        }

        // ---------- Core ----------

        static int MigrateOne(Component old, StringBuilder report, ref int strays)
        {
            var typeName = old.GetType().Name;

            if (IsOldPerRenderer(typeName))
            {
                var dst = old.gameObject.GetComponent<MeshSimplifier>();
                if (dst == null) dst = Undo.AddComponent<MeshSimplifier>(old.gameObject);
                CopyFields(old, dst, "target", "options");
                Undo.DestroyObjectImmediate(old); // replace in place
                report.AppendLine($"  • {Path(dst.gameObject)}  →  Mesh Simplifier");
                return 1;
            }
#if ENABLE_MODULAR_AVATAR
            if (IsOldCascading(typeName))
            {
                var scope = old.transform.parent;

                var dst = old.gameObject.GetComponent<AvatarMeshSimplifierTouma>();
                if (dst == null) dst = Undo.AddComponent<AvatarMeshSimplifierTouma>(old.gameObject);
                CopyFields(old, dst, "Entries", "TargetTriangleCount", "AutoAdjustEnabled");
                Undo.DestroyObjectImmediate(old); // replace in place

                // Keep exactly one new component in this scope.
                foreach (var other in FindInScope(scope))
                {
                    if (other == null || other == dst) continue;
                    var go = other.gameObject;
                    if (go != dst.gameObject && go.transform.childCount == 0 && go.GetComponents<Component>().Length == 2)
                        Undo.DestroyObjectImmediate(go); // dedicated empty holder -> drop the whole object
                    else
                        Undo.DestroyObjectImmediate(other);
                    strays++;
                }

                // If the object still carries an old default holder name, update it to match.
                if (dst.gameObject.name is "Meshia Cascading Avatar Mesh Simplifier" or "Cascading Avatar Mesh Simplifier")
                {
                    Undo.RecordObject(dst.gameObject, "Rename mesh-simplification object");
                    dst.gameObject.name = "Avatar Mesh Simplification - Touma Fork";
                }

                report.AppendLine($"  • {Path(dst.gameObject)}  →  Avatar Mesh Simplification - Touma Fork");
                return 1;
            }
#endif
            return 0;
        }

        static void ShowResult(int migrated, int strays, StringBuilder report)
        {
            if (migrated == 0)
            {
                EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                    "No old components found.\n\n" +
                    "The old components must still be present and loadable (old package installed).", "OK");
                return;
            }
            var tail = strays > 0 ? $"\nRemoved {strays} leftover/duplicate new component(s)." : "";
            EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                $"Migrated {migrated} component(s) in place:\n\n{report}{tail}\n" +
                "When everything looks right, uninstall the old package.", "OK");
        }

#if ENABLE_MODULAR_AVATAR
        static List<AvatarMeshSimplifierTouma> FindInScope(Transform? scope)
        {
            var result = new List<AvatarMeshSimplifierTouma>();
            if (scope == null) return result;
            foreach (var c in scope.GetComponentsInChildren<AvatarMeshSimplifierTouma>(true))
                if (c != null && c.transform.parent == scope) result.Add(c);
            return result;
        }
#endif

        static void CopyFields(Component src, Component dst, params string[] fields)
        {
            var so = new SerializedObject(src);
            var dso = new SerializedObject(dst);
            foreach (var field in fields)
            {
                var prop = so.FindProperty(field);
                if (prop != null) dso.CopyFromSerializedProperty(prop);
            }
            dso.ApplyModifiedProperties();
        }

        static string Path(GameObject go)
        {
            var t = go.transform;
            var path = go.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
