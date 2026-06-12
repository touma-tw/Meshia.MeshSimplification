using System;
using Unity.Mathematics;
namespace Touma.MeshSimplification
{
    struct VertexMerge : IComparable<VertexMerge>
    {
        public int VertexAIndex, VertexBIndex;
        public int VertexAVersion, VertexBVersion;
        public float3 Position;
        /// <summary>
        /// Optimal merged UV0 from the attribute aware solve. Only meaningful when
        /// <see cref="MeshSimplifierOptions.UseAttributeAwareError"/> is enabled.
        /// </summary>
        public float2 OptimalUv;
        public float Cost;

        public int CompareTo(VertexMerge other)
        {
            return Cost.CompareTo(other.Cost);
        }
    }
}


