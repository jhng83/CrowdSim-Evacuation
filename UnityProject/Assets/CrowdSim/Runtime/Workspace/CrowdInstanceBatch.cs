using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Persistent matrix blocks submitted as GPU-instanced volumes. No per-body-part Unity objects,
    /// colliders or simulation data. Materials/meshes are owned by the caller.</summary>
    public sealed class CrowdInstanceBatch
    {
        private sealed class Group
        {
            public readonly List<Matrix4x4[]> Blocks = new List<Matrix4x4[]>();
            public readonly Material Material;
            public readonly Mesh Mesh;
            public int Count;
            public Group(Material material, Mesh mesh) { Material = material; Mesh = mesh; }
        }
        private readonly Dictionary<(Material, Mesh), Group> _groups = new Dictionary<(Material, Mesh), Group>();
        private Bounds _bounds;
        private bool _hasBounds;
        public int InstanceCount { get; private set; }
        public int DrawCount { get; private set; }
        public void Reset()
        {
            foreach (Group group in _groups.Values) group.Count = 0;
            InstanceCount = DrawCount = 0; _hasBounds = false;
        }
        public void Clear() { _groups.Clear(); Reset(); }
        public void Add(Material material, Mesh mesh, Matrix4x4 matrix)
        {
            (Material, Mesh) key = (material, mesh);
            if (!_groups.TryGetValue(key, out Group group))
            {
                material.enableInstancing = true;
                group = new Group(material, mesh); _groups.Add(key, group);
            }
            int block = group.Count / InstanceBatchPlan.Capacity, slot = group.Count % InstanceBatchPlan.Capacity;
            if (block == group.Blocks.Count) group.Blocks.Add(new Matrix4x4[InstanceBatchPlan.Capacity]);
            group.Blocks[block][slot] = matrix; group.Count++; InstanceCount++;
            Vector3 position = matrix.GetColumn(3);
            if (!_hasBounds) { _bounds = new Bounds(position, Vector3.one * 6); _hasBounds = true; }
            else _bounds.Encapsulate(new Bounds(position, Vector3.one * 6));
        }
        public void Draw(Camera camera)
        {
            DrawCount = 0;
            foreach (Group group in _groups.Values)
            {
                if (group.Count == 0) continue;
                RenderParams parameters = new RenderParams(group.Material)
                {
                    camera = camera, worldBounds = _bounds, shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false, lightProbeUsage = LightProbeUsage.Off
                };
                for (int block = 0; block < InstanceBatchPlan.BatchCount(group.Count); block++)
                {
                    Graphics.RenderMeshInstanced(parameters, group.Mesh, 0, group.Blocks[block], InstanceBatchPlan.CountInBatch(group.Count, block));
                    DrawCount++;
                }
            }
        }
    }
}
