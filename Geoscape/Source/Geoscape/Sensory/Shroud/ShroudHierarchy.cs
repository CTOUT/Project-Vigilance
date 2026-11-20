using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory
{
    /// <summary>
    /// Delegate filling polygon vertices of a planet tile into a reusable scratch buffer.
    /// </summary>
    internal delegate void FillTileVerticesDelegate(int tileId, List<Vector3> outVertices);

    /// <summary>
    /// Delegate filling neighbour tile IDs of a planet tile into a reusable scratch buffer.
    /// </summary>
    internal delegate void FillTileNeighborsDelegate(int tileId, List<int> outNeighbors);

    /// <summary>
    /// Zero-allocation hierarchical fog-of-war state tracker mapping world hex/pent tiles
    /// onto subdivision-5 icosphere faces (Tier S) and lazy corner-fan tile chunks (Tier F).
    /// </summary>
    internal sealed class ShroudHierarchy
    {
        public const byte StateShroud = 0;
        public const byte StateHaze = 1;
        public const byte StateClear = 2;

        private readonly int _tileCount;
        private readonly int _faceCount;

        // CSR: F -> own(F) tiles overlapping face F
        private readonly int[] _ownOffsets;
        private readonly int[] _ownTiles;

        // CSR: F -> ring(F) 1-ring shroud collar around own(F), guaranteed disjoint from own(F)
        private readonly int[] _ringOffsets;
        private readonly int[] _ringTiles;

        // CSR: tile -> faces owned by tile
        private readonly int[] _tileFaceOffsets;
        private readonly int[] _tileFaces;

        // CSR: tile -> neighbours
        private readonly int[] _tileNeighborOffsets;
        private readonly int[] _tileNeighbors;

        // CSR: fine chunk (0..79) -> tiles rendered in that chunk
        private readonly int[] _fineChunkTileOffsets;
        private readonly int[] _fineChunkTiles;

        // CSR: fine chunk (0..79) -> adjacent fine chunks
        private readonly int[] _fineChunkAdjOffsets;
        private readonly int[] _fineChunkAdjChunks;

        private readonly int[] _tilePrimaryFineChunk;

        // Incremental state arrays and counters
        private readonly byte[] _tileStates;
        private readonly int[] _faceNonShroudCount;
        private readonly int[] _tileFineMembership;
        private readonly int[] _tileExploredNeighborCount;
        private readonly int[] _shellChunkCoarseCount;
        private readonly int[] _fineChunkFineTileCount;
        private readonly int[] _fineChunkVisibleTileCount;

        // Generation-stamped zero-alloc dirty tracking
        private readonly int[] _newlyFineStamp;
        private readonly int[] _newlyCoarseStamp;
        private readonly int[] _dirtyFineStamp;
        private readonly List<int> _newlyFineTileIds = new(256);
        private readonly List<int> _newlyCoarseTileIds = new(256);
        private readonly List<int> _dirtyFineTileIds = new(256);
        private int _flushGeneration = 1;
        private ulong _dirtyShellChunkMask;

        public int TileCount => _tileCount;
        public int FaceCount => _faceCount;
        public int ShellChunkCount => ShroudGeometry.ShellChunkCount;
        public int FineChunkCount => ShroudGeometry.FineChunkCount;

        public ulong DirtyShellChunkMask => _dirtyShellChunkMask;
        public IReadOnlyList<int> NewlyFineTileIds => _newlyFineTileIds;
        public IReadOnlyList<int> NewlyCoarseTileIds => _newlyCoarseTileIds;
        public IReadOnlyList<int> DirtyFineTileIds => _dirtyFineTileIds;

        public ShroudHierarchy(
            in ShroudGeometry.IcoSphere sphere,
            int tileCount,
            Func<int, Vector3> getTileCenter,
            FillTileVerticesDelegate fillTileVertices,
            FillTileNeighborsDelegate fillTileNeighbors)
        {
            if (tileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileCount));
            }

            if (getTileCenter == null)
            {
                throw new ArgumentNullException(nameof(getTileCenter));
            }

            if (fillTileVertices == null)
            {
                throw new ArgumentNullException(nameof(fillTileVertices));
            }

            if (fillTileNeighbors == null)
            {
                throw new ArgumentNullException(nameof(fillTileNeighbors));
            }

            _tileCount = tileCount;
            _faceCount = sphere.FaceCount;

            _tileStates = new byte[tileCount];
            _faceNonShroudCount = new int[_faceCount];
            _tileFineMembership = new int[tileCount];
            _tileExploredNeighborCount = new int[tileCount];
            _shellChunkCoarseCount = new int[ShroudGeometry.ShellChunkCount];
            _fineChunkFineTileCount = new int[ShroudGeometry.FineChunkCount];
            _fineChunkVisibleTileCount = new int[ShroudGeometry.FineChunkCount];

            _newlyFineStamp = new int[tileCount];
            _newlyCoarseStamp = new int[tileCount];
            _dirtyFineStamp = new int[tileCount];

            Array.Fill(_shellChunkCoarseCount, ShroudGeometry.FacesPerShellChunk);

            _tilePrimaryFineChunk = new int[tileCount];

            // Step 1: Build tile -> neighbor CSR
            var neighborScratch = new List<int>(8);
            var neighborOffsets = new int[tileCount + 1];
            var neighborList = new List<int>(tileCount * 6);
            var seenNeighbor = new int[tileCount];
            Array.Fill(seenNeighbor, -1);

            for (int t = 0; t < tileCount; t++)
            {
                neighborOffsets[t] = neighborList.Count;
                neighborScratch.Clear();
                fillTileNeighbors(t, neighborScratch);
                for (int i = 0; i < neighborScratch.Count; i++)
                {
                    int n = neighborScratch[i];
                    if ((uint)n >= (uint)tileCount || n == t || seenNeighbor[n] == t)
                    {
                        continue;
                    }

                    seenNeighbor[n] = t;
                    neighborList.Add(n);
                }
            }

            neighborOffsets[tileCount] = neighborList.Count;
            _tileNeighborOffsets = neighborOffsets;
            _tileNeighbors = neighborList.ToArray();

            // Step 2: Map each tile to primary FineChunk and distinct owned sub-5 faces
            var vertsScratch = new List<Vector3>(8);
            var tileFaceOffsets = new int[tileCount + 1];
            var tileFaceList = new List<int>(Math.Max(16, tileCount * 3));
            var ownCounts = new int[_faceCount];
            var seenFace = new int[_faceCount];
            Array.Fill(seenFace, -1);

            var fineChunkCounts = new int[ShroudGeometry.FineChunkCount];
            bool checkLargeTileInterior = tileCount > 0 && tileCount <= 1024;

            for (int t = 0; t < tileCount; t++)
            {
                tileFaceOffsets[t] = tileFaceList.Count;

                Vector3 center = getTileCenter(t);
                Vector3 centerDir = center.sqrMagnitude > 1e-12f ? center.normalized : Vector3.up;
                int centerFace = ShroudGeometry.FindOwningFace(in sphere, centerDir);
                int primaryFineChunk = ShroudGeometry.GetFineChunk(centerFace);
                _tilePrimaryFineChunk[t] = primaryFineChunk;
                fineChunkCounts[primaryFineChunk]++;

                seenFace[centerFace] = t;
                tileFaceList.Add(centerFace);
                ownCounts[centerFace]++;

                vertsScratch.Clear();
                fillTileVertices(t, vertsScratch);
                for (int i = 0; i < vertsScratch.Count; i++)
                {
                    Vector3 v = vertsScratch[i];
                    if (v.sqrMagnitude <= 1e-12f)
                    {
                        continue;
                    }

                    Vector3 vDir = v.normalized;
                    int face = ShroudGeometry.FindOwningFace(in sphere, vDir);
                    if (seenFace[face] != t)
                    {
                        seenFace[face] = t;
                        tileFaceList.Add(face);
                        ownCounts[face]++;
                    }
                }

                // On very coarse synthetic test globes, large tiles can enclose whole sub-5 faces inside their interior.
                if (checkLargeTileInterior && vertsScratch.Count >= 3)
                {
                    AddInteriorFacesForCoarseTile(
                        in sphere,
                        t,
                        centerDir,
                        vertsScratch,
                        seenFace,
                        tileFaceList,
                        ownCounts);
                }
            }

            tileFaceOffsets[tileCount] = tileFaceList.Count;
            _tileFaceOffsets = tileFaceOffsets;
            _tileFaces = tileFaceList.ToArray();

            // Step 3: Invert tile -> faces into CSR face -> own(F)
            var ownOffsets = new int[_faceCount + 1];
            int totalOwn = 0;
            for (int f = 0; f < _faceCount; f++)
            {
                ownOffsets[f] = totalOwn;
                totalOwn += ownCounts[f];
            }

            ownOffsets[_faceCount] = totalOwn;
            var ownTiles = new int[totalOwn];
            var ownWritePos = new int[_faceCount];
            Array.Copy(ownOffsets, ownWritePos, _faceCount);

            for (int t = 0; t < tileCount; t++)
            {
                int start = tileFaceOffsets[t];
                int end = tileFaceOffsets[t + 1];
                for (int idx = start; idx < end; idx++)
                {
                    int f = _tileFaces[idx];
                    ownTiles[ownWritePos[f]++] = t;
                }
            }

            _ownOffsets = ownOffsets;
            _ownTiles = ownTiles;

            // Step 4: Build disjoint 1-ring collar CSR face -> ring(F)
            var ownMark = new int[tileCount];
            var ringMark = new int[tileCount];
            Array.Fill(ownMark, -1);
            Array.Fill(ringMark, -1);

            var ringOffsets = new int[_faceCount + 1];
            var ringList = new List<int>(Math.Max(64, totalOwn));

            for (int f = 0; f < _faceCount; f++)
            {
                ringOffsets[f] = ringList.Count;
                int ownStart = ownOffsets[f];
                int ownEnd = ownOffsets[f + 1];
                if (ownStart == ownEnd)
                {
                    continue;
                }

                for (int oi = ownStart; oi < ownEnd; oi++)
                {
                    ownMark[ownTiles[oi]] = f;
                }

                for (int oi = ownStart; oi < ownEnd; oi++)
                {
                    int t = ownTiles[oi];
                    int nStart = _tileNeighborOffsets[t];
                    int nEnd = _tileNeighborOffsets[t + 1];
                    for (int ni = nStart; ni < nEnd; ni++)
                    {
                        int n = _tileNeighbors[ni];
                        if (ownMark[n] == f || ringMark[n] == f)
                        {
                            continue;
                        }

                        ringMark[n] = f;
                        ringList.Add(n);
                    }
                }
            }

            ringOffsets[_faceCount] = ringList.Count;
            _ringOffsets = ringOffsets;
            _ringTiles = ringList.ToArray();

            // Step 5: Build fineChunk (0..79) -> tiles CSR
            var fineChunkTileOffsets = new int[ShroudGeometry.FineChunkCount + 1];
            int totalChunkTiles = 0;
            for (int c = 0; c < ShroudGeometry.FineChunkCount; c++)
            {
                fineChunkTileOffsets[c] = totalChunkTiles;
                totalChunkTiles += fineChunkCounts[c];
            }

            fineChunkTileOffsets[ShroudGeometry.FineChunkCount] = totalChunkTiles;
            var fineChunkTiles = new int[tileCount];
            var chunkWritePos = new int[ShroudGeometry.FineChunkCount];
            Array.Copy(fineChunkTileOffsets, chunkWritePos, ShroudGeometry.FineChunkCount);

            for (int t = 0; t < tileCount; t++)
            {
                int c = _tilePrimaryFineChunk[t];
                fineChunkTiles[chunkWritePos[c]++] = t;
            }

            _fineChunkTileOffsets = fineChunkTileOffsets;
            _fineChunkTiles = fineChunkTiles;

            // Step 6: Build fineChunk (0..79) -> adjacent fine chunks CSR
            BuildFineChunkAdjacency(in sphere, out _fineChunkAdjOffsets, out _fineChunkAdjChunks);
        }

        public byte GetTileState(int tileId)
        {
            return (uint)tileId < (uint)_tileCount ? _tileStates[tileId] : StateShroud;
        }

        public bool IsFaceCoarse(int faceIndex)
        {
            return (uint)faceIndex < (uint)_faceCount && _faceNonShroudCount[faceIndex] == 0;
        }

        public bool IsTileFine(int tileId)
        {
            return (uint)tileId < (uint)_tileCount && _tileFineMembership[tileId] > 0;
        }

        public bool IsFrontierRing(int tileId)
        {
            return (uint)tileId < (uint)_tileCount
                && _tileStates[tileId] == StateShroud
                && _tileExploredNeighborCount[tileId] > 0;
        }

        public bool HasExploredNeighbor(int tileId)
        {
            return (uint)tileId < (uint)_tileCount && _tileExploredNeighborCount[tileId] > 0;
        }

        public int GetTileFineChunk(int tileId)
        {
            return (uint)tileId < (uint)_tileCount ? _tilePrimaryFineChunk[tileId] : 0;
        }

        public int GetShellChunkCoarseCount(int chunkIndex)
        {
            return (uint)chunkIndex < (uint)ShroudGeometry.ShellChunkCount ? _shellChunkCoarseCount[chunkIndex] : 0;
        }

        public int GetFineChunkFineTileCount(int chunkIndex)
        {
            return (uint)chunkIndex < (uint)ShroudGeometry.FineChunkCount ? _fineChunkFineTileCount[chunkIndex] : 0;
        }

        public int GetFineChunkVisibleTileCount(int chunkIndex)
        {
            return (uint)chunkIndex < (uint)ShroudGeometry.FineChunkCount ? _fineChunkVisibleTileCount[chunkIndex] : 0;
        }

        public ReadOnlySpan<int> GetOwnTiles(int faceIndex)
        {
            if ((uint)faceIndex >= (uint)_faceCount)
            {
                return ReadOnlySpan<int>.Empty;
            }

            int start = _ownOffsets[faceIndex];
            int length = _ownOffsets[faceIndex + 1] - start;
            return _ownTiles.AsSpan(start, length);
        }

        public ReadOnlySpan<int> GetRingTiles(int faceIndex)
        {
            if ((uint)faceIndex >= (uint)_faceCount)
            {
                return ReadOnlySpan<int>.Empty;
            }

            int start = _ringOffsets[faceIndex];
            int length = _ringOffsets[faceIndex + 1] - start;
            return _ringTiles.AsSpan(start, length);
        }

        public ReadOnlySpan<int> GetTileFaces(int tileId)
        {
            if ((uint)tileId >= (uint)_tileCount)
            {
                return ReadOnlySpan<int>.Empty;
            }

            int start = _tileFaceOffsets[tileId];
            int length = _tileFaceOffsets[tileId + 1] - start;
            return _tileFaces.AsSpan(start, length);
        }

        public ReadOnlySpan<int> GetFineChunkTiles(int chunkIndex)
        {
            if ((uint)chunkIndex >= (uint)ShroudGeometry.FineChunkCount)
            {
                return ReadOnlySpan<int>.Empty;
            }

            int start = _fineChunkTileOffsets[chunkIndex];
            int length = _fineChunkTileOffsets[chunkIndex + 1] - start;
            return _fineChunkTiles.AsSpan(start, length);
        }

        public ReadOnlySpan<int> GetFineChunkAdjacentChunks(int chunkIndex)
        {
            if ((uint)chunkIndex >= (uint)ShroudGeometry.FineChunkCount)
            {
                return ReadOnlySpan<int>.Empty;
            }

            int start = _fineChunkAdjOffsets[chunkIndex];
            int length = _fineChunkAdjOffsets[chunkIndex + 1] - start;
            return _fineChunkAdjChunks.AsSpan(start, length);
        }

        /// <summary>
        /// Transitions <paramref name="tileId"/> to <paramref name="newState"/> incrementally
        /// with zero heap allocations. Returns true if the tile's state changed.
        /// </summary>
        public bool SetTileState(int tileId, byte newState)
        {
            if ((uint)tileId >= (uint)_tileCount)
            {
                return false;
            }

            byte oldState = _tileStates[tileId];
            if (oldState == newState)
            {
                return false;
            }

            bool wasNonShroud = oldState != StateShroud;
            bool isNonShroud = newState != StateShroud;
            bool wasClear = oldState == StateClear;
            bool isClear = newState == StateClear;
            bool wasFineSelf = _tileFineMembership[tileId] > 0;

            _tileStates[tileId] = newState;

            if (wasFineSelf)
            {
                if (wasClear != isClear)
                {
                    int selfChunk = _tilePrimaryFineChunk[tileId];
                    _fineChunkVisibleTileCount[selfChunk] += isClear ? -1 : 1;
                }

                MarkFineDirty(tileId);
            }

            if (wasNonShroud == isNonShroud)
            {
                return true;
            }

            int nStart = _tileNeighborOffsets[tileId];
            int nEnd = _tileNeighborOffsets[tileId + 1];

            if (!wasNonShroud)
            {
                // Transition Shroud -> Explored/Clear
                for (int ni = nStart; ni < nEnd; ni++)
                {
                    int neighbor = _tileNeighbors[ni];
                    int expCount = ++_tileExploredNeighborCount[neighbor];
                    if (expCount == 1 && _tileStates[neighbor] == StateShroud && _tileFineMembership[neighbor] > 0)
                    {
                        MarkFineDirty(neighbor);
                    }
                }

                int fStart = _tileFaceOffsets[tileId];
                int fEnd = _tileFaceOffsets[tileId + 1];
                for (int fi = fStart; fi < fEnd; fi++)
                {
                    int face = _tileFaces[fi];
                    if (++_faceNonShroudCount[face] != 1)
                    {
                        continue;
                    }

                    int shellChunk = ShroudGeometry.GetShellChunk(face);
                    _shellChunkCoarseCount[shellChunk]--;
                    _dirtyShellChunkMask |= 1UL << shellChunk;

                    IncrementFaceMembership(face);
                }
            }
            else
            {
                // Transition Explored/Clear -> Shroud
                for (int ni = nStart; ni < nEnd; ni++)
                {
                    int neighbor = _tileNeighbors[ni];
                    int expCount = --_tileExploredNeighborCount[neighbor];
                    if (expCount == 0 && _tileStates[neighbor] == StateShroud && _tileFineMembership[neighbor] > 0)
                    {
                        MarkFineDirty(neighbor);
                    }
                }

                int fStart = _tileFaceOffsets[tileId];
                int fEnd = _tileFaceOffsets[tileId + 1];
                for (int fi = fStart; fi < fEnd; fi++)
                {
                    int face = _tileFaces[fi];
                    if (--_faceNonShroudCount[face] != 0)
                    {
                        continue;
                    }

                    int shellChunk = ShroudGeometry.GetShellChunk(face);
                    _shellChunkCoarseCount[shellChunk]++;
                    _dirtyShellChunkMask |= 1UL << shellChunk;

                    DecrementFaceMembership(face);
                }
            }

            return true;
        }

        public ulong ConsumeShellChunkDirtyMask()
        {
            ulong mask = _dirtyShellChunkMask;
            _dirtyShellChunkMask = 0UL;
            return mask;
        }

        public void ClearDirtyFlags()
        {
            _dirtyShellChunkMask = 0UL;
            _newlyFineTileIds.Clear();
            _newlyCoarseTileIds.Clear();
            _dirtyFineTileIds.Clear();

            if (_flushGeneration == int.MaxValue)
            {
                Array.Clear(_newlyFineStamp, 0, _newlyFineStamp.Length);
                Array.Clear(_newlyCoarseStamp, 0, _newlyCoarseStamp.Length);
                Array.Clear(_dirtyFineStamp, 0, _dirtyFineStamp.Length);
                _flushGeneration = 1;
            }
            else
            {
                _flushGeneration++;
            }
        }

        private void IncrementFaceMembership(int face)
        {
            int ownStart = _ownOffsets[face];
            int ownEnd = _ownOffsets[face + 1];
            for (int i = ownStart; i < ownEnd; i++)
            {
                IncrementTileFineMembership(_ownTiles[i]);
            }

            int ringStart = _ringOffsets[face];
            int ringEnd = _ringOffsets[face + 1];
            for (int i = ringStart; i < ringEnd; i++)
            {
                IncrementTileFineMembership(_ringTiles[i]);
            }
        }

        private void DecrementFaceMembership(int face)
        {
            int ownStart = _ownOffsets[face];
            int ownEnd = _ownOffsets[face + 1];
            for (int i = ownStart; i < ownEnd; i++)
            {
                DecrementTileFineMembership(_ownTiles[i]);
            }

            int ringStart = _ringOffsets[face];
            int ringEnd = _ringOffsets[face + 1];
            for (int i = ringStart; i < ringEnd; i++)
            {
                DecrementTileFineMembership(_ringTiles[i]);
            }
        }

        private void IncrementTileFineMembership(int tile)
        {
            if (++_tileFineMembership[tile] != 1)
            {
                return;
            }

            int fineChunk = _tilePrimaryFineChunk[tile];
            _fineChunkFineTileCount[fineChunk]++;
            if (_tileStates[tile] != StateClear)
            {
                _fineChunkVisibleTileCount[fineChunk]++;
            }

            if (_newlyFineStamp[tile] != _flushGeneration)
            {
                _newlyFineStamp[tile] = _flushGeneration;
                _newlyFineTileIds.Add(tile);
            }

            MarkFineDirty(tile);
        }

        private void DecrementTileFineMembership(int tile)
        {
            if (--_tileFineMembership[tile] != 0)
            {
                return;
            }

            int fineChunk = _tilePrimaryFineChunk[tile];
            _fineChunkFineTileCount[fineChunk]--;
            if (_tileStates[tile] != StateClear)
            {
                _fineChunkVisibleTileCount[fineChunk]--;
            }

            if (_newlyCoarseStamp[tile] != _flushGeneration)
            {
                _newlyCoarseStamp[tile] = _flushGeneration;
                _newlyCoarseTileIds.Add(tile);
            }
        }

        private void MarkFineDirty(int tile)
        {
            if (_dirtyFineStamp[tile] != _flushGeneration)
            {
                _dirtyFineStamp[tile] = _flushGeneration;
                _dirtyFineTileIds.Add(tile);
            }
        }

        private void BuildFineChunkAdjacency(
            in ShroudGeometry.IcoSphere sphere,
            out int[] adjOffsets,
            out int[] adjChunks)
        {
            const int count = ShroudGeometry.FineChunkCount;
            bool[,] matrix = new bool[count, count];

            // Geometric adjacency: fine chunks 0..79 match level-1 faces at Hierarchy offset 20..99
            const int level1Offset = 20;
            for (int c0 = 0; c0 < count; c0++)
            {
                int a0 = sphere.HierarchyV0[level1Offset + c0];
                int a1 = sphere.HierarchyV1[level1Offset + c0];
                int a2 = sphere.HierarchyV2[level1Offset + c0];

                for (int c1 = c0 + 1; c1 < count; c1++)
                {
                    int b0 = sphere.HierarchyV0[level1Offset + c1];
                    int b1 = sphere.HierarchyV1[level1Offset + c1];
                    int b2 = sphere.HierarchyV2[level1Offset + c1];

                    if (a0 == b0 || a0 == b1 || a0 == b2
                        || a1 == b0 || a1 == b1 || a1 == b2
                        || a2 == b0 || a2 == b1 || a2 == b2)
                    {
                        matrix[c0, c1] = true;
                        matrix[c1, c0] = true;
                    }
                }
            }

            // Topological adjacency: neighbouring planet tiles crossing chunk boundaries
            for (int t = 0; t < _tileCount; t++)
            {
                int c0 = _tilePrimaryFineChunk[t];
                int start = _tileNeighborOffsets[t];
                int end = _tileNeighborOffsets[t + 1];
                for (int i = start; i < end; i++)
                {
                    int c1 = _tilePrimaryFineChunk[_tileNeighbors[i]];
                    if (c0 != c1)
                    {
                        matrix[c0, c1] = true;
                        matrix[c1, c0] = true;
                    }
                }
            }

            adjOffsets = new int[count + 1];
            var list = new List<int>(count * 12);
            for (int c = 0; c < count; c++)
            {
                adjOffsets[c] = list.Count;
                for (int other = 0; other < count; other++)
                {
                    if (other != c && matrix[c, other])
                    {
                        list.Add(other);
                    }
                }
            }

            adjOffsets[count] = list.Count;
            adjChunks = list.ToArray();
        }

        private static void AddInteriorFacesForCoarseTile(
            in ShroudGeometry.IcoSphere sphere,
            int tileId,
            Vector3 tileCenterDir,
            List<Vector3> verts,
            int[] seenFace,
            List<int> tileFaceList,
            int[] ownCounts)
        {
            int vertCount = verts.Count;
            float minDot = 1f;
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 v = verts[i].normalized;
                float d = Vector3.Dot(v, tileCenterDir);
                if (d < minDot)
                {
                    minDot = d;
                }
            }

            // Skip oversized/degenerate patches (e.g., unit-test dummy quads that span huge angles)
            if (minDot < 0.7f)
            {
                return;
            }

            for (int f = 0; f < sphere.FaceCount; f++)
            {
                if (seenFace[f] == tileId)
                {
                    continue;
                }

                Vector3 faceDir = sphere.FaceCentroids[f];
                if (Vector3.Dot(faceDir, tileCenterDir) < minDot - 0.01f)
                {
                    continue;
                }

                if (IsPointInsideSphericalPolygon(faceDir, tileCenterDir, verts))
                {
                    seenFace[f] = tileId;
                    tileFaceList.Add(f);
                    ownCounts[f]++;
                }
            }
        }

        private static bool IsPointInsideSphericalPolygon(Vector3 pointDir, Vector3 centerDir, List<Vector3> verts)
        {
            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = verts[i].normalized;
                Vector3 b = verts[(i + 1) % n].normalized;
                Vector3 edgeNormal = Vector3.Cross(a, b);
                if (edgeNormal.sqrMagnitude <= 1e-12f)
                {
                    continue;
                }

                // Orient plane inward toward tile center
                if (Vector3.Dot(edgeNormal, centerDir) < 0f)
                {
                    edgeNormal = -edgeNormal;
                }

                if (Vector3.Dot(edgeNormal, pointDir) < -1e-4f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
