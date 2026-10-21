using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdSim.Unity.Workspace
{
    internal sealed class GroundOverlayLayer
    {
        private readonly GameObject _root;
        private readonly Mesh _fill, _line;
        private readonly Material _fillMaterial, _lineMaterial;
        private readonly GameObject _fillHost, _lineHost;
        private BlastMeshData _shown;
        public GroundOverlayLayer(string name, Transform parent)
        {
            _root = new GameObject(name); _root.transform.SetParent(parent, false);
            _fill = new Mesh { name = name + " fill" }; _line = new Mesh { name = name + " outline" };
            _fillMaterial = CreateMaterial(); _lineMaterial = CreateMaterial();
            _fillHost = Host("Fill", _fill, _fillMaterial); _lineHost = Host("Outline", _line, _lineMaterial);
        }
        private GameObject Host(string name, Mesh mesh, Material material)
        {
            GameObject host = new GameObject(name); host.transform.SetParent(_root.transform, false);
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = host.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return host;
        }
        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader is unavailable.");
            Material material = new Material(shader);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_Cull", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            return material;
        }
        public void Draw(BlastMeshData data, bool visible, float fillAlpha, float lineAlpha, bool showLines = true)
        {
            _root.SetActive(visible && data != null);
            if (data == null) { _shown = null; return; }
            _fillHost.SetActive(data.Quads.Count > 0); _lineHost.SetActive(showLines && data.Lines.Count > 0);
            if (ReferenceEquals(data, _shown)) return;
            _shown = data;
            Build(_fill, data.Quads, false); Build(_line, data.Lines, true);
            _fillMaterial.color = new Color(data.Color.X, data.Color.Y, data.Color.Z, fillAlpha);
            _lineMaterial.color = new Color(data.Color.X, data.Color.Y, data.Color.Z, lineAlpha);

        }
        private static void Build(Mesh mesh, IReadOnlyList<System.Numerics.Vector3> source, bool lines)
        {
            mesh.Clear();
            mesh.indexFormat = source.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            Vector3[] vertices = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++) vertices[i] = new Vector3(source[i].X, source[i].Y, source[i].Z);
            int[] indices = new int[lines ? source.Count : source.Count / 4 * 6];
            if (lines) for (int i = 0; i < indices.Length; i++) indices[i] = i;
            else for (int q = 0; q < source.Count / 4; q++)
            {
                int p = q * 4, offset = q * 6;
                indices[offset] = p; indices[offset + 1] = p + 1; indices[offset + 2] = p + 2;
                indices[offset + 3] = p; indices[offset + 4] = p + 2; indices[offset + 5] = p + 3;
            }
            mesh.vertices = vertices;
            mesh.SetIndices(indices, lines ? MeshTopology.Lines : MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
        }
        public void Dispose()
        { UnityEngine.Object.Destroy(_fill); UnityEngine.Object.Destroy(_line); UnityEngine.Object.Destroy(_fillMaterial); UnityEngine.Object.Destroy(_lineMaterial); }
    }
}
