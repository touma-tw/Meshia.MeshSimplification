#nullable enable
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Touma.MeshSimplification.Ndmf.Editor
{
    /// <summary>
    /// One-shot migration helper: copies settings from the original Meshia components
    /// (or any earlier-named build of this fork) onto the current Touma-fork components.
    ///
    /// Works by serialized-field copy keyed on type *name*, so it needs no compile-time
    /// reference to the old assembly. The old package must still be installed (old
    /// components must be loadable, not "missing script") while migrating.
    /// </summary>
    static class SettingsMigrationTool
    {
        const string Root = "Tools/Mesh Simplification - Touma Fork/";
        const string MigratePath = Root + "Migrate old settings (selected hierarchy)";
        const string CleanupPath = Root + "Remove old components (selected hierarchy)";

        // Type *names* of components produced by older versions / the original Meshia.
        static bool IsOldCascading(string typeName) =>
            typeName is "MeshiaCascadingAvatarMeshSimplifier" or "CascadingAvatarMeshSimplifier";

        static bool IsOldPerRenderer(string typeName) =>
            typeName is "MeshiaMeshSimplifier";

        [MenuItem(MigratePath, true)]
        [MenuItem(CleanupPath, true)]
        static bool Validate() => Selection.activeGameObject != null;

        [MenuItem(MigratePath, false)]
        static void Migrate()
        {
            var root = Selection.activeGameObject;
            if (root == null) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Migrate mesh-simplification settings");

            int migrated = 0;
            var report = new StringBuilder();

            foreach (var old in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (old == null) continue;
                var typeName = old.GetType().Name;

                if (IsOldPerRenderer(typeName))
                {
                    var dst = old.gameObject.GetComponent<MeshSimplifier>();
                    if (dst == null) dst = Undo.AddComponent<MeshSimplifier>(old.gameObject);
                    CopyFields(old, dst, "target", "options");
                    report.AppendLine($"  • {Path(old.gameObject)}  →  Mesh Simplifier");
                    migrated++;
                }
#if ENABLE_MODULAR_AVATAR
                else if (IsOldCascading(typeName))
                {
                    var dst = old.gameObject.GetComponent<AvatarMeshSimplifierTouma>();
                    if (dst == null) dst = Undo.AddComponent<AvatarMeshSimplifierTouma>(old.gameObject);
                    CopyFields(old, dst, "Entries", "TargetTriangleCount", "AutoAdjustEnabled");
                    report.AppendLine($"  • {Path(old.gameObject)}  →  Avatar Mesh Simplification - Touma Fork");
                    migrated++;
                }
#endif
            }

            if (migrated == 0)
            {
                EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                    "No old components found under the selection.\n\n" +
                    "Keep the old package installed (its components must not be \"missing script\"), " +
                    "select the avatar root, then run this again.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                $"Migrated {migrated} component(s):\n\n{report}\n" +
                "The old components were left in place. After verifying the new ones, " +
                "run \"Remove old components\" and then uninstall the old package.", "OK");
        }

        [MenuItem(CleanupPath, false)]
        static void RemoveOld()
        {
            var root = Selection.activeGameObject;
            if (root == null) return;

            int removed = 0;
            foreach (var old in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (old == null) continue;
                var typeName = old.GetType().Name;
                if (IsOldPerRenderer(typeName) || IsOldCascading(typeName))
                {
                    Undo.DestroyObjectImmediate(old);
                    removed++;
                }
            }
            EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                removed == 0 ? "No old components found under the selection." : $"Removed {removed} old component(s).", "OK");
        }

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
