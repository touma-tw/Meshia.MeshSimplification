using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Touma.MeshSimplification
{
    [BurstCompile]
    struct ComputeMergesJob : IJobParallelForDefer
    {
        [ReadOnly]
        public NativeArray<float3> VertexPositionBuffer;
        [ReadOnly]
        public NativeArray<ErrorQuadric> VertexErrorQuadrics;
        [ReadOnly]
        public NativeArray<AttributeErrorQuadric> VertexAttributeErrorQuadrics;
        [ReadOnly]
        public NativeArray<float4> VertexTexCoord0Buffer;
        [ReadOnly]
        public NativeArray<float3> TriangleNormals;
        [ReadOnly]
        public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        [ReadOnly]
        public NativeBitArray VertexIsBorderEdgeBits;
        [ReadOnly]
        public NativeBitArray VertexIsUVSeamBits;
        [ReadOnly]
        public NativeArray<int2> Edges;
        [ReadOnly]
        public NativeArray<uint> VertexBlendIndicesBuffer;
        [ReadOnly]
        public NativeArray<uint> VertexContainingSubMeshIndices;

        [ReadOnly]
        public NativeBitArray PreserveBorderEdgesBoneIndices;
        [WriteOnly]
        public NativeArray<VertexMerge> UnorderedDirtyVertexMerges;
        public bool PreserveBorderEdges;
        public bool PreserveSurfaceCurvature;
        public bool PreserveSubMeshBoundaries;
        public bool PreserveUVSeams;
        public bool ConstrainOptimalPosition;
        public float MaxCollapseDisplacementFactor;
        public bool UseAttributeAwareError;
        public float UvErrorWeight;
        public void Execute(int index)
        {
            var mergeFactory = new MergeFactory
            {
                VertexPositionBuffer = VertexPositionBuffer,
                VertexErrorQuadrics = VertexErrorQuadrics,
                VertexAttributeErrorQuadrics = VertexAttributeErrorQuadrics,
                VertexTexCoord0Buffer = VertexTexCoord0Buffer,
                VertexContainingTriangles = VertexContainingTriangles,
                VertexIsBorderEdgeBits = VertexIsBorderEdgeBits,
                VertexIsUVSeamBits = VertexIsUVSeamBits,
                TriangleNormals = TriangleNormals,
                PreserveBorderEdges = PreserveBorderEdges,
                PreserveSurfaceCurvature = PreserveSurfaceCurvature,
                PreserveSubMeshBoundaries = PreserveSubMeshBoundaries,
                PreserveUVSeams = PreserveUVSeams,
                ConstrainOptimalPosition = ConstrainOptimalPosition,
                MaxCollapseDisplacementFactor = MaxCollapseDisplacementFactor,
                UseAttributeAwareError = UseAttributeAwareError,
                UvErrorWeight = UvErrorWeight,
                PreserveBorderEdgesBoneIndices = PreserveBorderEdgesBoneIndices,
                VertexBlendIndicesBuffer = VertexBlendIndicesBuffer,
                VertexContainingSubMeshIndices = VertexContainingSubMeshIndices,
            };
            var edge = Edges[index];
            VertexMerge merge;
            if (mergeFactory.TryComputeMerge(edge, out var position, out var optimalUv, out var cost))
            {
                merge = new()
                {
                    VertexAIndex = edge.x,
                    VertexBIndex = edge.y,
                    VertexAVersion = 0,
                    VertexBVersion = 0,
                    Position = position,
                    OptimalUv = optimalUv,
                    Cost = cost,
                };

            }
            else
            {

                merge = new()
                {
                    VertexAIndex = edge.x,
                    VertexBIndex = edge.y,
                    VertexAVersion = 0,
                    VertexBVersion = 0,
                    Position = float.NaN,
                    OptimalUv = float.NaN,
                    Cost = float.PositiveInfinity,
                };
            }
            UnorderedDirtyVertexMerges[index] = merge;

        }
    }
}


