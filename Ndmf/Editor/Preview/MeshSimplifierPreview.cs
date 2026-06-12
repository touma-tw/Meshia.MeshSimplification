#nullable enable

using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace Touma.MeshSimplification.Ndmf.Editor.Preview
{
    internal class MeshSimplifierPreview : MeshSimplifierPreviewBase<MeshSimplifierPreview>
    {
        public override ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            return context.GetComponentsByType<MeshSimplifier>()
            .Where(meshiaMeshSimplifier => context.ActiveAndEnabled(meshiaMeshSimplifier))
            .Select(meshiaMeshSimplifier => context.GetComponent<Renderer>(meshiaMeshSimplifier.gameObject))
            .Where(renderer => renderer is MeshRenderer or SkinnedMeshRenderer)
            .Select(renderer => RenderGroup.For(renderer))
            .ToImmutableList();
        }
        protected override (MeshSimplificationTarget, MeshSimplifierOptions, BitArray?) QueryTarget(ComputeContext context, RenderGroup group, Renderer original, Renderer proxy)
        {
            var ndmfMeshSimplifier = original.GetComponent<MeshSimplifier>();
            var target = context.Observe(ndmfMeshSimplifier, ndmfMeshSimplifier => ndmfMeshSimplifier.target, (x, y) => x == y);
            var options = context.Observe(ndmfMeshSimplifier, ndmfMeshSimplifier => ndmfMeshSimplifier.options, (x, y) => x == y);
            return (target, options, null);
        }
    }
}