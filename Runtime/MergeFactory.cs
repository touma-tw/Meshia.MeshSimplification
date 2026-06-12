using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
namespace Touma.MeshSimplification
{
    struct MergeFactory
    {
        public NativeArray<float3> VertexPositionBuffer;
        public NativeArray<uint> VertexBlendIndicesBuffer;
        public NativeArray<ErrorQuadric> VertexErrorQuadrics;
        public NativeArray<AttributeErrorQuadric> VertexAttributeErrorQuadrics;
        public NativeArray<float4> VertexTexCoord0Buffer;
        public NativeParallelMultiHashMap<int, int> VertexContainingTriangles;
        public NativeBitArray VertexIsBorderEdgeBits;
        public NativeBitArray VertexIsUVSeamBits;
        public NativeBitArray PreserveBorderEdgesBoneIndices;
        public NativeArray<float3> TriangleNormals;
        /// <summary>
        /// Per vertex bit mask of the sub meshes (materials) the vertex belongs to.
        /// Empty when sub mesh boundary preservation is disabled.
        /// </summary>
        public NativeArray<uint> VertexContainingSubMeshIndices;
        public bool PreserveBorderEdges;
        public bool PreserveSurfaceCurvature;
        public bool PreserveSubMeshBoundaries;
        public bool PreserveUVSeams;
        public bool ConstrainOptimalPosition;
        public float MaxCollapseDisplacementFactor;
        public bool UseAttributeAwareError;
        public float UvErrorWeight;

        PreservedVertexPredicator PreservedVertexPredicator => new()
        {
            VertexBlendIndicesBuffer = VertexBlendIndicesBuffer,
            VertexIsBorderEdgeBits = VertexIsBorderEdgeBits,
            VertexIsUVSeamBits = VertexIsUVSeamBits,
            PreserveBorderEdgesBoneIndices = PreserveBorderEdgesBoneIndices,
            VertexBoneCount = VertexBlendIndicesBuffer.Length / VertexPositionBuffer.Length,
            PreserveBorderEdges = PreserveBorderEdges,
            PreserveUVSeams = PreserveUVSeams,
        };

        [BurstCompile]
        static class ProfilerMarkers
        {
            public static readonly ProfilerMarker TryComputeMerge = new(nameof(TryComputeMerge));
            public static readonly ProfilerMarker ComputeCurvatureError = new(nameof(ComputeCurvatureError));
        }

        public bool TryComputeMerge(int2 vertices, out float3 position, out float2 optimalUv, out float cost)
        {
            using (ProfilerMarkers.TryComputeMerge.Auto())
            {
                optimalUv = default;

                // Material boundary lock: never merge vertices that belong to a different set of
                // sub meshes (materials). Merging across a material boundary corrupts the per sub mesh
                // vertex ranges reconstructed in WriteToMeshDataJob (the bit mask is not updated after a merge)
                // and bleeds materials across their boundary.
                if (PreserveSubMeshBoundaries
                    && VertexContainingSubMeshIndices.Length != 0
                    && VertexContainingSubMeshIndices[vertices.x] != VertexContainingSubMeshIndices[vertices.y])
                {
                    position = float.NaN;
                    optimalUv = float.NaN;
                    cost = float.PositiveInfinity;
                    return false;
                }

                // Attribute aware path: include UV0 in the error metric and solve position + UV together.
                if (UseAttributeAwareError && UvErrorWeight > 0f && VertexAttributeErrorQuadrics.Length != 0)
                {
                    return TryComputeMergeAttributeAware(vertices, out position, out optimalUv, out cost);
                }

                var q = VertexErrorQuadrics[vertices.x] + VertexErrorQuadrics[vertices.y];

                var positionX = VertexPositionBuffer[vertices.x];
                var positionY = VertexPositionBuffer[vertices.y];

                float vertexError;

                var preservedVertexPredicator = PreservedVertexPredicator;

                var preserveX = preservedVertexPredicator.IsPreserved(vertices.x);
                var preserveY = preservedVertexPredicator.IsPreserved(vertices.y);
                if (preserveX && preserveY)
                {
                    position = float.NaN;
                    cost = float.PositiveInfinity;
                    return false;
                }
                else if (preserveX)
                {
                    position = positionX;
                    goto ComputeVertexError;
                }
                else if (preserveY)
                {
                    position = positionY;
                    goto ComputeVertexError;
                }

                var determinant = q.Determinant1();
                var hasOptimalPosition = determinant != 0;
                if (hasOptimalPosition)
                {
                    var optimalPosition = new float3
                    {
                        x = -1 / determinant * q.Determinant2(),
                        y = 1 / determinant * q.Determinant3(),
                        z = -1 / determinant * q.Determinant4(),
                    };

                    // Anti self-intersection: the unconstrained quadric optimum can fly far away from the
                    // collapsed edge (especially on flat or near singular regions), producing spikes that poke
                    // through other surfaces. Reject it when it lands outside the local neighborhood of the edge
                    // and fall back to the endpoint/midpoint candidates instead.
                    if (ConstrainOptimalPosition)
                    {
                        var edgeMidpoint = (positionX + positionY) * 0.5f;
                        var maxDisplacement = MaxCollapseDisplacementFactor * math.distance(positionX, positionY);
                        if (math.distance(optimalPosition, edgeMidpoint) > maxDisplacement)
                        {
                            hasOptimalPosition = false;
                        }
                    }

                    if (hasOptimalPosition)
                    {
                        position = optimalPosition;
                        goto ComputeVertexError;
                    }
                }

                {
                    var positionZ = (positionX + positionY) * 0.5f;
                    var errorX = q.ComputeError(positionX);
                    var errorY = q.ComputeError(positionY);
                    var errorZ = q.ComputeError(positionZ);

                    if (errorX < errorY)
                    {
                        if (errorX < errorZ)
                        {
                            position = positionX;
                            vertexError = errorX;

                        }
                        else
                        {
                            position = positionZ;
                            vertexError = errorZ;
                        }
                    }
                    else
                    {
                        if (errorY < errorZ)
                        {
                            position = positionY;
                            vertexError = errorY;
                        }
                        else
                        {
                            position = positionZ;
                            vertexError = errorZ;
                        }
                    }

                    goto ApplyCurvatureError;
                }


            ComputeVertexError:
                vertexError = q.ComputeError(position);

            ApplyCurvatureError:
                var curvatureError = PreserveSurfaceCurvature ? ComputeCurvatureError(vertices) : 0;

                cost = vertexError + curvatureError;
                return true;
            }

        }
        bool TryComputeMergeAttributeAware(int2 vertices, out float3 position, out float2 optimalUv, out float cost)
        {
            var q = VertexAttributeErrorQuadrics[vertices.x] + VertexAttributeErrorQuadrics[vertices.y];
            var weight = UvErrorWeight;

            var positionX = VertexPositionBuffer[vertices.x];
            var positionY = VertexPositionBuffer[vertices.y];
            var uvX = VertexTexCoord0Buffer.Length != 0 ? VertexTexCoord0Buffer[vertices.x].xy : float2.zero;
            var uvY = VertexTexCoord0Buffer.Length != 0 ? VertexTexCoord0Buffer[vertices.y].xy : float2.zero;

            var preservedVertexPredicator = PreservedVertexPredicator;
            var preserveX = preservedVertexPredicator.IsPreserved(vertices.x);
            var preserveY = preservedVertexPredicator.IsPreserved(vertices.y);

            if (preserveX && preserveY)
            {
                position = float.NaN;
                optimalUv = float.NaN;
                cost = float.PositiveInfinity;
                return false;
            }
            else if (preserveX)
            {
                position = positionX;
                optimalUv = uvX;
                cost = q.ComputeError(positionX, uvX * weight) + CurvatureError(vertices);
                return true;
            }
            else if (preserveY)
            {
                position = positionY;
                optimalUv = uvY;
                cost = q.ComputeError(positionY, uvY * weight) + CurvatureError(vertices);
                return true;
            }

            var solved = q.TrySolveOptimal(out var solvedPosition, out var solvedWeightedUv);

            // Anti self-intersection: reject the optimum if it flies away from the collapsed edge.
            if (solved && ConstrainOptimalPosition)
            {
                var edgeMidpoint = (positionX + positionY) * 0.5f;
                var maxDisplacement = MaxCollapseDisplacementFactor * math.distance(positionX, positionY);
                if (math.distance(solvedPosition, edgeMidpoint) > maxDisplacement)
                {
                    solved = false;
                }
            }

            if (solved)
            {
                position = solvedPosition;
                optimalUv = solvedWeightedUv / weight;
                cost = q.ComputeError(solvedPosition, solvedWeightedUv);
            }
            else
            {
                // Fall back to the endpoint / midpoint candidates evaluated with their own UVs.
                var midpoint = (positionX + positionY) * 0.5f;
                var uvMid = (uvX + uvY) * 0.5f;

                var errorX = q.ComputeError(positionX, uvX * weight);
                var errorY = q.ComputeError(positionY, uvY * weight);
                var errorMid = q.ComputeError(midpoint, uvMid * weight);

                if (errorX <= errorY && errorX <= errorMid)
                {
                    position = positionX;
                    optimalUv = uvX;
                    cost = errorX;
                }
                else if (errorY <= errorMid)
                {
                    position = positionY;
                    optimalUv = uvY;
                    cost = errorY;
                }
                else
                {
                    position = midpoint;
                    optimalUv = uvMid;
                    cost = errorMid;
                }
            }

            cost += CurvatureError(vertices);
            return true;
        }

        float CurvatureError(int2 vertices) => PreserveSurfaceCurvature ? ComputeCurvatureError(vertices) : 0f;

        float ComputeCurvatureError(int2 vertices)
        {

            using (ProfilerMarkers.ComputeCurvatureError.Auto())
            {
                var distance = math.distance(VertexPositionBuffer[vertices.x], VertexPositionBuffer[vertices.y]);
                using UnsafeHashSet<int> vertexXContainingTriangles = new(8, Allocator.Temp);

                using UnsafeList<int> vertexXOrYContainingTriangles = new(16, Allocator.Temp);


                foreach (var vertexXContainingTriangle in VertexContainingTriangles.GetValuesForKey(vertices.x))
                {
                    vertexXContainingTriangles.Add(vertexXContainingTriangle);
                    vertexXOrYContainingTriangles.Add(vertexXContainingTriangle);
                }


                using UnsafeList<int> vertexXAndYContainingTriangles = new(8, Allocator.Temp);

                foreach (var vertexYContainingTriangle in VertexContainingTriangles.GetValuesForKey(vertices.y))
                {
                    if (vertexXContainingTriangles.Contains(vertexYContainingTriangle))
                    {
                        vertexXAndYContainingTriangles.Add(vertexYContainingTriangle);
                    }
                    else
                    {
                        vertexXOrYContainingTriangles.Add(vertexYContainingTriangle);
                    }
                }

                vertexXContainingTriangles.Dispose();

                var maxDot = 0f;

                foreach (var vertexXOrYContainingTriangle in vertexXOrYContainingTriangles)
                {
                    var vertexXOrYContainingTriangleNormal = TriangleNormals[vertexXOrYContainingTriangle];

                    foreach (var vertexXAndYContainingTriangle in vertexXAndYContainingTriangles)
                    {
                        var vertexXAndYContainingTriangleNormal = TriangleNormals[vertexXAndYContainingTriangle];
                        var dot = math.dot(vertexXOrYContainingTriangleNormal, vertexXAndYContainingTriangleNormal);
                        maxDot = math.max(dot, maxDot);
                    }
                }
                return distance * maxDot;
            }
        }
    }
}


