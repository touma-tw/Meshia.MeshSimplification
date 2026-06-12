using UnityEngine;


namespace Touma.MeshSimplification.Ndmf
{
    [AddComponentMenu("Mesh Simplification/Mesh Simplifier")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class MeshSimplifier : MonoBehaviour
#if ENABLE_VRCHAT_BASE
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public MeshSimplificationTarget target = new()
        {
            Kind = MeshSimplificationTargetKind.RelativeVertexCount,
            Value = 0.5f,
        };
        public MeshSimplifierOptions options = MeshSimplifierOptions.Default;

        void Start() { } // To show enabled checkbox in inspector
    }

}

