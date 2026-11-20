using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory
{
    /// <summary>
    /// Precomputed subdivision-5 icosphere topology and per-chunk local geometry
    /// backing Tier S (coarse shroud shell) and Tier F (fine chunk binning).
    /// </summary>
    internal static class ShroudGeometry
    {
        public const int Subdivisions = 5;
        public const int BaseFaceCount = 20;
        public const int ShellChunkCount = 20;
        public const int FineChunkCount = 320;
        public const int FacesPerShellChunk = 1024;
        public const int FacesPerFineChunk = 64;
        public const int TotalFaceCount = BaseFaceCount * FacesPerShellChunk;
        public const int TotalVertexCount = 10242;

        private const int IntermediateFaceCount = 20 + 80 + 320 + 1280 + 5120;
        private static readonly int[] LevelOffsets = { 0, 20, 100, 420, 1700 };

        private static readonly object SharedLock = new();
        private static IcoSphere? s_sharedSphere;

        /// <summary>
        /// Precomputed deduplicated local vertices and triangle indices for a single base-face shell chunk.
        /// </summary>
        internal sealed class ShellChunkGeometry
        {
            public readonly int ChunkIndex;
            public readonly int StartFace;
            public readonly int EndFaceExclusive;
            public readonly int[] GlobalVertexIds;
            public readonly Vector3[] LocalDirections;
            public readonly int[] FaceLocalV0;
            public readonly int[] FaceLocalV1;
            public readonly int[] FaceLocalV2;

            public int LocalVertexCount => GlobalVertexIds.Length;

            internal ShellChunkGeometry(
                int chunkIndex,
                int startFace,
                int endFaceExclusive,
                int[] globalVertexIds,
                Vector3[] localDirections,
                int[] faceLocalV0,
                int[] faceLocalV1,
                int[] faceLocalV2)
            {
                ChunkIndex = chunkIndex;
                StartFace = startFace;
                EndFaceExclusive = endFaceExclusive;
                GlobalVertexIds = globalVertexIds;
                LocalDirections = localDirections;
                FaceLocalV0 = faceLocalV0;
                FaceLocalV1 = faceLocalV1;
                FaceLocalV2 = faceLocalV2;
            }
        }

        /// <summary>
        /// Immutable subdivision-5 icosphere geometry and hierarchical lookup tables.
        /// </summary>
        internal readonly struct IcoSphere
        {
            public readonly Vector3[] Vertices;
            public readonly int[] FaceV0;
            public readonly int[] FaceV1;
            public readonly int[] FaceV2;
            public readonly Vector3[] FaceCentroids;
            public readonly ShellChunkGeometry[] ShellChunks;

            // Flattened levels 0..4 (excluding leaf level 5) for fast hierarchical point location.
            internal readonly int[] HierarchyV0;
            internal readonly int[] HierarchyV1;
            internal readonly int[] HierarchyV2;

            public int FaceCount => FaceV0.Length;
            public int VertexCount => Vertices.Length;

            internal IcoSphere(
                Vector3[] vertices,
                int[] faceV0,
                int[] faceV1,
                int[] faceV2,
                Vector3[] faceCentroids,
                ShellChunkGeometry[] shellChunks,
                int[] hierarchyV0,
                int[] hierarchyV1,
                int[] hierarchyV2)
            {
                Vertices = vertices;
                FaceV0 = faceV0;
                FaceV1 = faceV1;
                FaceV2 = faceV2;
                FaceCentroids = faceCentroids;
                ShellChunks = shellChunks;
                HierarchyV0 = hierarchyV0;
                HierarchyV1 = hierarchyV1;
                HierarchyV2 = hierarchyV2;
            }
        }

        /// <summary>
        /// Returns the cached immutable subdivision-5 icosphere, building it on first access.
        /// </summary>
        public static IcoSphere GetSharedSphere()
        {
            lock (SharedLock)
            {
                s_sharedSphere ??= BuildIcoSphere();
                return s_sharedSphere.Value;
            }
        }

        /// <summary>
        /// Returns the owning Tier S chunk index (0..19) for a subdivision-5 face.
        /// </summary>
        public static int GetShellChunk(int faceIndex)
        {
            return faceIndex >> 10;
        }

        /// <summary>
        /// Returns the owning Tier F chunk index (0..319) for a subdivision-5 face.
        /// </summary>
        public static int GetFineChunk(int faceIndex)
        {
            return faceIndex >> 6;
        }

        /// <summary>
        /// Builds a complete subdivision-5 icosphere with guaranteed outward triangle winding
        /// and deduplicated per-chunk local vertex tables.
        /// </summary>
        public static IcoSphere BuildIcoSphere()
        {
            var vertices = new List<Vector3>(TotalVertexCount);
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

            AddNormalizedVertex(vertices, -1f, t, 0f);
            AddNormalizedVertex(vertices, 1f, t, 0f);
            AddNormalizedVertex(vertices, -1f, -t, 0f);
            AddNormalizedVertex(vertices, 1f, -t, 0f);

            AddNormalizedVertex(vertices, 0f, -1f, t);
            AddNormalizedVertex(vertices, 0f, 1f, t);
            AddNormalizedVertex(vertices, 0f, -1f, -t);
            AddNormalizedVertex(vertices, 0f, 1f, -t);

            AddNormalizedVertex(vertices, t, 0f, -1f);
            AddNormalizedVertex(vertices, t, 0f, 1f);
            AddNormalizedVertex(vertices, -t, 0f, -1f);
            AddNormalizedVertex(vertices, -t, 0f, 1f);

            int[] currentV0 = new int[BaseFaceCount];
            int[] currentV1 = new int[BaseFaceCount];
            int[] currentV2 = new int[BaseFaceCount];

            ReadOnlySpan<int> baseIndices = new[]
            {
                0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
            };

            for (int f = 0; f < BaseFaceCount; f++)
            {
                int a = baseIndices[f * 3];
                int b = baseIndices[f * 3 + 1];
                int c = baseIndices[f * 3 + 2];
                EnsureOutwardWinding(vertices, a, ref b, ref c);
                currentV0[f] = a;
                currentV1[f] = b;
                currentV2[f] = c;
            }

            int[] hierarchyV0 = new int[IntermediateFaceCount];
            int[] hierarchyV1 = new int[IntermediateFaceCount];
            int[] hierarchyV2 = new int[IntermediateFaceCount];

            var midpointCache = new Dictionary<long, int>(TotalVertexCount * 3);

            for (int level = 0; level < Subdivisions; level++)
            {
                int faceCount = currentV0.Length;
                int offset = LevelOffsets[level];
                Array.Copy(currentV0, 0, hierarchyV0, offset, faceCount);
                Array.Copy(currentV1, 0, hierarchyV1, offset, faceCount);
                Array.Copy(currentV2, 0, hierarchyV2, offset, faceCount);

                int nextFaceCount = faceCount * 4;
                int[] nextV0 = new int[nextFaceCount];
                int[] nextV1 = new int[nextFaceCount];
                int[] nextV2 = new int[nextFaceCount];

                midpointCache.Clear();

                for (int f = 0; f < faceCount; f++)
                {
                    int i0 = currentV0[f];
                    int i1 = currentV1[f];
                    int i2 = currentV2[f];

                    int m01 = GetOrCreateMidpoint(vertices, midpointCache, i0, i1);
                    int m12 = GetOrCreateMidpoint(vertices, midpointCache, i1, i2);
                    int m20 = GetOrCreateMidpoint(vertices, midpointCache, i2, i0);

                    int dst = f * 4;
                    SetTriangleOutward(vertices, nextV0, nextV1, nextV2, dst, i0, m01, m20);
                    SetTriangleOutward(vertices, nextV0, nextV1, nextV2, dst + 1, i1, m12, m01);
                    SetTriangleOutward(vertices, nextV0, nextV1, nextV2, dst + 2, i2, m20, m12);
                    SetTriangleOutward(vertices, nextV0, nextV1, nextV2, dst + 3, m01, m12, m20);
                }

                currentV0 = nextV0;
                currentV1 = nextV1;
                currentV2 = nextV2;
            }

            Vector3[] vertexArray = vertices.ToArray();
            var centroids = new Vector3[TotalFaceCount];
            for (int f = 0; f < TotalFaceCount; f++)
            {
                Vector3 c = vertexArray[currentV0[f]]
                          + vertexArray[currentV1[f]]
                          + vertexArray[currentV2[f]];
                centroids[f] = c.sqrMagnitude > 1e-12f ? c.normalized : Vector3.up;
            }

            ShellChunkGeometry[] shellChunks = BuildShellChunkGeometries(vertexArray, currentV0, currentV1, currentV2);

            return new IcoSphere(
                vertexArray,
                currentV0,
                currentV1,
                currentV2,
                centroids,
                shellChunks,
                hierarchyV0,
                hierarchyV1,
                hierarchyV2);
        }

        /// <summary>
        /// Finds the subdivision-5 face index owning <paramref name="unitDir"/> using exact hierarchical
        /// spherical triangle containment with centroid dot-product tie-breaking and zero heap allocations.
        /// </summary>
        public static int FindOwningFace(in IcoSphere sphere, Vector3 unitDir)
        {
            float magSq = unitDir.sqrMagnitude;
            if (magSq > 1e-12f && Mathf.Abs(magSq - 1f) > 1e-5f)
            {
                unitDir /= Mathf.Sqrt(magSq);
            }

            int bestBaseFace = 0;
            float bestBaseMargin = float.NegativeInfinity;
            float bestBaseDot = float.NegativeInfinity;

            for (int f = 0; f < BaseFaceCount; f++)
            {
                EvaluateFaceContainment(
                    sphere.Vertices,
                    sphere.HierarchyV0[f],
                    sphere.HierarchyV1[f],
                    sphere.HierarchyV2[f],
                    unitDir,
                    out float margin,
                    out float centroidDot);

                if (IsBetterCandidate(margin, centroidDot, bestBaseMargin, bestBaseDot))
                {
                    bestBaseMargin = margin;
                    bestBaseDot = centroidDot;
                    bestBaseFace = f;
                }
            }

            int candidateFace = DescendFromBaseFace(in sphere, bestBaseFace, unitDir, out float leafMargin, out float leafDot);

            // If point falls right near a base-face edge, check neighbouring base faces with non-negative margin tolerance.
            if (leafMargin < -1e-5f)
            {
                for (int f = 0; f < BaseFaceCount; f++)
                {
                    if (f == bestBaseFace)
                    {
                        continue;
                    }

                    EvaluateFaceContainment(
                        sphere.Vertices,
                        sphere.HierarchyV0[f],
                        sphere.HierarchyV1[f],
                        sphere.HierarchyV2[f],
                        unitDir,
                        out float margin,
                        out _);

                    if (margin < -1e-3f)
                    {
                        continue;
                    }

                    int altLeaf = DescendFromBaseFace(in sphere, f, unitDir, out float altMargin, out float altDot);
                    if (IsBetterCandidate(altMargin, altDot, leafMargin, leafDot))
                    {
                        leafMargin = altMargin;
                        leafDot = altDot;
                        candidateFace = altLeaf;
                    }
                }
            }

            return candidateFace;
        }

        private static int DescendFromBaseFace(
            in IcoSphere sphere,
            int baseFace,
            Vector3 unitDir,
            out float finalMargin,
            out float finalDot)
        {
            int idxInLevel = baseFace;
            for (int level = 0; level < Subdivisions - 1; level++)
            {
                int childOffset = LevelOffsets[level + 1];
                int firstChild = idxInLevel * 4;
                int bestChild = firstChild;
                float bestMargin = float.NegativeInfinity;
                float bestDot = float.NegativeInfinity;

                for (int k = 0; k < 4; k++)
                {
                    int childLocal = firstChild + k;
                    int hIdx = childOffset + childLocal;
                    EvaluateFaceContainment(
                        sphere.Vertices,
                        sphere.HierarchyV0[hIdx],
                        sphere.HierarchyV1[hIdx],
                        sphere.HierarchyV2[hIdx],
                        unitDir,
                        out float margin,
                        out float centroidDot);

                    if (IsBetterCandidate(margin, centroidDot, bestMargin, bestDot))
                    {
                        bestMargin = margin;
                        bestDot = centroidDot;
                        bestChild = childLocal;
                    }
                }

                idxInLevel = bestChild;
            }

            int firstLeaf = idxInLevel * 4;
            int bestLeaf = firstLeaf;
            finalMargin = float.NegativeInfinity;
            finalDot = float.NegativeInfinity;

            for (int k = 0; k < 4; k++)
            {
                int leaf = firstLeaf + k;
                EvaluateFaceContainment(
                    sphere.Vertices,
                    sphere.FaceV0[leaf],
                    sphere.FaceV1[leaf],
                    sphere.FaceV2[leaf],
                    unitDir,
                    out float margin,
                    out float centroidDot);

                if (IsBetterCandidate(margin, centroidDot, finalMargin, finalDot))
                {
                    finalMargin = margin;
                    finalDot = centroidDot;
                    bestLeaf = leaf;
                }
            }

            return bestLeaf;
        }

        private static void EvaluateFaceContainment(
            Vector3[] vertices,
            int v0Idx,
            int v1Idx,
            int v2Idx,
            Vector3 unitDir,
            out float minEdgeMargin,
            out float centroidDot)
        {
            Vector3 a = vertices[v0Idx];
            Vector3 b = vertices[v1Idx];
            Vector3 c = vertices[v2Idx];

            float e0 = Vector3.Dot(Vector3.Cross(a, b), unitDir);
            float e1 = Vector3.Dot(Vector3.Cross(b, c), unitDir);
            float e2 = Vector3.Dot(Vector3.Cross(c, a), unitDir);

            minEdgeMargin = e0 < e1 ? (e0 < e2 ? e0 : e2) : (e1 < e2 ? e1 : e2);
            Vector3 sum = a + b + c;
            float invLen = 1f / Mathf.Max(1e-6f, sum.magnitude);
            centroidDot = Vector3.Dot(sum, unitDir) * invLen;
        }

        private static bool IsBetterCandidate(float margin, float dot, float bestMargin, float bestDot)
        {
            const float tieEpsilon = 1e-6f;
            if (margin > bestMargin + tieEpsilon)
            {
                return true;
            }

            if (bestMargin > margin + tieEpsilon)
            {
                return false;
            }

            return dot > bestDot;
        }

        private static void AddNormalizedVertex(List<Vector3> vertices, float x, float y, float z)
        {
            vertices.Add(new Vector3(x, y, z).normalized);
        }

        private static int GetOrCreateMidpoint(
            List<Vector3> vertices,
            Dictionary<long, int> cache,
            int i0,
            int i1)
        {
            int min = i0 < i1 ? i0 : i1;
            int max = i0 < i1 ? i1 : i0;
            long key = ((long)min << 32) | (uint)max;

            if (cache.TryGetValue(key, out int existing))
            {
                return existing;
            }

            Vector3 mid = (vertices[i0] + vertices[i1]).normalized;
            int index = vertices.Count;
            vertices.Add(mid);
            cache[key] = index;
            return index;
        }

        private static void SetTriangleOutward(
            List<Vector3> vertices,
            int[] dstV0,
            int[] dstV1,
            int[] dstV2,
            int dstIndex,
            int a,
            int b,
            int c)
        {
            EnsureOutwardWinding(vertices, a, ref b, ref c);
            dstV0[dstIndex] = a;
            dstV1[dstIndex] = b;
            dstV2[dstIndex] = c;
        }

        private static void EnsureOutwardWinding(List<Vector3> vertices, int a, ref int b, ref int c)
        {
            Vector3 va = vertices[a];
            Vector3 vb = vertices[b];
            Vector3 vc = vertices[c];
            Vector3 outward = va + vb + vc;
            if (Vector3.Dot(Vector3.Cross(vb - va, vc - va), outward) < 0f)
            {
                int tmp = b;
                b = c;
                c = tmp;
            }
        }

        private static ShellChunkGeometry[] BuildShellChunkGeometries(
            Vector3[] vertices,
            int[] faceV0,
            int[] faceV1,
            int[] faceV2)
        {
            var chunks = new ShellChunkGeometry[ShellChunkCount];
            var globalToLocal = new int[vertices.Length];
            var localGlobals = new List<int>(600);

            for (int c = 0; c < ShellChunkCount; c++)
            {
                Array.Fill(globalToLocal, -1);
                localGlobals.Clear();

                int startFace = c * FacesPerShellChunk;
                int endFace = startFace + FacesPerShellChunk;
                int[] localV0 = new int[FacesPerShellChunk];
                int[] localV1 = new int[FacesPerShellChunk];
                int[] localV2 = new int[FacesPerShellChunk];

                for (int i = 0; i < FacesPerShellChunk; i++)
                {
                    int f = startFace + i;
                    localV0[i] = MapLocalVertex(faceV0[f], globalToLocal, localGlobals);
                    localV1[i] = MapLocalVertex(faceV1[f], globalToLocal, localGlobals);
                    localV2[i] = MapLocalVertex(faceV2[f], globalToLocal, localGlobals);
                }

                int count = localGlobals.Count;
                int[] globalIds = localGlobals.ToArray();
                var localDirs = new Vector3[count];
                for (int v = 0; v < count; v++)
                {
                    localDirs[v] = vertices[globalIds[v]];
                }

                chunks[c] = new ShellChunkGeometry(
                    c,
                    startFace,
                    endFace,
                    globalIds,
                    localDirs,
                    localV0,
                    localV1,
                    localV2);
            }

            return chunks;
        }

        private static int MapLocalVertex(int globalVertexId, int[] globalToLocal, List<int> localGlobals)
        {
            int localId = globalToLocal[globalVertexId];
            if (localId >= 0)
            {
                return localId;
            }

            localId = localGlobals.Count;
            globalToLocal[globalVertexId] = localId;
            localGlobals.Add(globalVertexId);
            return localId;
        }
    }
}
