#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touma.MeshSimplification.Ndmf.Editor
{
    [CustomEditor(typeof(MeshSimplifier))]
    [CanEditMultipleObjects]
    public class MeshSimplifierEditor : UnityEditor.Editor
    {
        [SerializeField]
        VisualTreeAsset visualTreeAsset = null!;
        
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            visualTreeAsset.CloneTree(root);
            root.Bind(serializedObject);

            var ndmfNotImportedWarning = root.Q<HelpBox>("NdmfNotImportedWarning");
            DisplayStyle warningDisplayStyle;
#if ENABLE_NDMF
            warningDisplayStyle = DisplayStyle.None;
#else
            warningDisplayStyle = DisplayStyle.Flex;
#endif
            ndmfNotImportedWarning.style.display = warningDisplayStyle;

            // Live readout of the actual resulting triangle/vertex count. Preserve options
            // (UV seams, surface curvature, material boundaries, ...) can prevent reaching the
            // requested target, so showing the real count makes tuning intuitive.
            var resultCountReadout = new HelpBox(string.Empty, HelpBoxMessageType.None)
            {
                name = "ResultCountReadout",
            };
            root.Add(resultCountReadout);

            void UpdateResultCountReadout() => resultCountReadout.text = ComputeResultCountReadout();

            UpdateResultCountReadout();

            // Debounce so dragging a slider over a heavy mesh does not run a full simplify every frame.
            IVisualElementScheduledItem? scheduledUpdate = null;
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                scheduledUpdate?.Pause();
                scheduledUpdate = root.schedule.Execute(UpdateResultCountReadout).StartingIn(250);
            });

            var bakeMeshButtonContainer = root.Q<IMGUIContainer>("BakeMeshButtonContainer");
            bakeMeshButtonContainer.onGUIHandler = () =>
            {
                // TODO: Replace this with non-IMGUI implementation
                // But how could we register callback for whether target mesh is currently available?
                if (targets.Length == 1)
                {
                    var ndmfMeshSimplifier = (MeshSimplifier)target;
                    if (TryGetTargetMesh(ndmfMeshSimplifier, out var targetMesh))
                    {
                        if (GUILayout.Button("Bake mesh"))
                        {
                            var absolutePath = EditorUtility.SaveFilePanel(
                                        title: "Save baked mesh",
                                        directory: "",
                                        defaultName: $"{targetMesh.name}-Simplified.asset",
                                        extension: "asset");

                            if (!string.IsNullOrEmpty(absolutePath))
                            {
                                Mesh simplifiedMesh = new();

                                global::Touma.MeshSimplification.MeshSimplifier.Simplify(targetMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);

                                AssetDatabase.CreateAsset(simplifiedMesh, Path.Join("Assets/", Path.GetRelativePath(Application.dataPath, absolutePath)));
                            }
                        }
                    }

                }
            };
            
            return root;
        }

        private string ComputeResultCountReadout()
        {
            if (targets.Length != 1)
            {
                return "Select a single Mesh Simplifier to see the resulting triangle count.";
            }
            var ndmfMeshSimplifier = (MeshSimplifier)target;
            if (!TryGetTargetMesh(ndmfMeshSimplifier, out var targetMesh))
            {
                return "No mesh found on this object.";
            }

            var sourceTriangleCount = CountTriangles(targetMesh);

            Mesh simplifiedMesh = new();
            try
            {
                global::Touma.MeshSimplification.MeshSimplifier.Simplify(targetMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);
                var resultTriangleCount = CountTriangles(simplifiedMesh);
                var resultVertexCount = simplifiedMesh.vertexCount;
                var targetDescription = DescribeTarget(ndmfMeshSimplifier.target, sourceTriangleCount, targetMesh.vertexCount);
                return $"Result: {resultTriangleCount:N0} triangles / {resultVertexCount:N0} vertices\n" +
                       $"Source: {sourceTriangleCount:N0} triangles / {targetMesh.vertexCount:N0} vertices  (target: {targetDescription})";
            }
            catch (System.Exception exception)
            {
                return $"Failed to compute the resulting count: {exception.Message}";
            }
            finally
            {
                DestroyImmediate(simplifiedMesh);
            }
        }

        private static int CountTriangles(Mesh mesh)
        {
            var triangleCount = 0;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                if (mesh.GetTopology(subMeshIndex) == MeshTopology.Triangles)
                {
                    triangleCount += (int)(mesh.GetIndexCount(subMeshIndex) / 3);
                }
            }
            return triangleCount;
        }

        private static string DescribeTarget(MeshSimplificationTarget target, int sourceTriangleCount, int sourceVertexCount) => target.Kind switch
        {
            MeshSimplificationTargetKind.RelativeTriangleCount => $"{(int)(sourceTriangleCount * target.Value):N0} triangles ({target.Value:P0})",
            MeshSimplificationTargetKind.AbsoluteTriangleCount => $"{(int)target.Value:N0} triangles",
            MeshSimplificationTargetKind.RelativeVertexCount => $"{(int)(sourceVertexCount * target.Value):N0} vertices ({target.Value:P0})",
            MeshSimplificationTargetKind.AbsoluteVertexCount => $"{(int)target.Value:N0} vertices",
            MeshSimplificationTargetKind.ScaledTotalError => $"scaled error {target.Value}",
            MeshSimplificationTargetKind.AbsoluteTotalError => $"absolute error {target.Value}",
            _ => target.Value.ToString(),
        };

        private static bool TryGetTargetMesh(MeshSimplifier ndmfMeshSimplifier, [NotNullWhen(true)] out Mesh? targetMesh)
        {
            targetMesh = null;
            if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                targetMesh = meshFilter.sharedMesh;
                if (targetMesh != null) 
                {
                    return true;
                }
            }
            if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
            {
                targetMesh = skinnedMeshRenderer.sharedMesh; 
                if (targetMesh != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
