#nullable enable
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Touma.MeshSimplification.Ndmf.Editor
{
    /// <summary>
    /// One-shot migration helper: replaces the original Meshia components (or any
    /// earlier-named build of this fork) in place with the current Touma-fork components,
    /// carrying their settings across.
    ///
    /// Works by serialized-field copy keyed on type *name*, so it needs no compile-time
    /// reference to the old assembly. The old package must still be installed (old
    /// components must be loadable, not "missing script") while migrating.
    ///
    /// Workflow: select the avatar root and run "Migrate". Do NOT pre-create any new
    /// component object first — the tool puts the new component where the old one was.
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

            int migrated = 0, strays = 0;
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
                    Undo.DestroyObjectImmediate(old); // replace in place
                    report.AppendLine($"  • {Path(dst.gameObject)}  →  Mesh Simplifier");
                    migrated++;
                }
#if ENABLE_MODULAR_AVATAR
                else if (IsOldCascading(typeName))
                {
                    // Cascading component's scope is its parent, and only one per scope is allowed.
                    var scope = old.transform.parent;

                    var dst = old.gameObject.GetComponent<AvatarMeshSimplifierTouma>();
                    if (dst == null) dst = Undo.AddComponent<AvatarMeshSimplifierTouma>(old.gameObject);
                    CopyFields(old, dst, "Entries", "TargetTriangleCount", "AutoAdjustEnabled");
                    Undo.DestroyObjectImmediate(old); // replace in place

                    // Remove any other new component in the same scope (e.g. an empty one created
                    // earlier by hand), so exactly one remains and the scope stays valid.
                    foreach (var other in FindInScope(root, scope))
                    {
                        if (other == null || other == dst) continue;
                        var go = other.gameObject;
                        if (go != dst.gameObject && go.transform.childCount == 0 && go.GetComponents<Component>().Length == 2)
                            Undo.DestroyObjectImmediate(go); // dedicated empty holder -> drop the whole object
                        else
                            Undo.DestroyObjectImmediate(other);
                        strays++;
                    }

                    report.AppendLine($"  • {Path(dst.gameObject)}  →  Avatar Mesh Simplification - Touma Fork");
                    migrated++;
                }
#endif
            }

            if (migrated == 0)
            {
                EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                    "No old components found under the selection.\n\n" +
                    "Select the avatar root (with the OLD components still present and the old " +
                    "package still installed), then run this again. You do not need to create any " +
                    "new component first.", "OK");
                return;
            }

            var tail = strays > 0 ? $"\nRemoved {strays} leftover/duplicate new component(s)." : "";
            EditorUtility.DisplayDialog("Mesh Simplification - Touma Fork",
                $"Migrated {migrated} component(s) in place:\n\n{report}{tail}\n" +
                "When everything looks right, uninstall the old package.", "OK");
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

#if ENABLE_MODULAR_AVATAR
        static List<AvatarMeshSimplifierTouma> FindInScope(GameObject root, Transform? scope)
        {
            var result = new List<AvatarMeshSimplifierTouma>();
            foreach (var c in root.GetComponentsInChildren<AvatarMeshSimplifierTouma>(true))
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
