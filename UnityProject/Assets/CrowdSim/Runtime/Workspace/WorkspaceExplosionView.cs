using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Transparent ground meshes from detached values. No particle, collider or rendering callback edits simulation state.</summary>
    public sealed class WorkspaceExplosionView : MonoBehaviour
    {
        private readonly Dictionary<string, Layer> _layers = new Dictionary<string, Layer>();
        public void Draw(WorkspaceSession session)
        {
            ExplosionFrame frame = session.Blast;
            for (int i = 0; i < 5; i++)
                Display("Blast zone " + i, i < frame.Zones.Count ? frame.Zones[i] : null,
                    session.ShowBlastZones && session.ZoneVisibility[i], .075f, .68f);
            Display("Blast shock", frame.Shock, session.ShowShockFront, .16f, .74f);
            for (int i = 0; i < 8; i++)
                Display("Blast dust " + i, i < frame.Dust.Count ? frame.Dust[i] : null,
                    session.ShowDust, i < frame.Dust.Count ? frame.Dust[i].Color.W : 0, 0);
        }
        private void Display(string name, BlastMeshData data, bool visible, float fillAlpha, float lineAlpha)
        {
            if (!_layers.TryGetValue(name, out Layer layer))
            {
                if (data == null) return;
                layer = new Layer(name, transform); _layers.Add(name, layer);
            }
            layer.Draw(data, visible, fillAlpha, lineAlpha);
        }
        private void OnDestroy()
        { foreach (Layer layer in _layers.Values) layer.Dispose(); }

        private sealed class Layer
        {
            private readonly GameObject _root;
            private readonly Mesh _fill, _line;
            private readonly Material _fillMaterial, _lineMaterial;
            private readonly GameObject _fillHost, _lineHost;
            private BlastMeshData _shown;
            public Layer(string name, Transform parent)
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
            public void Draw(BlastMeshData data, bool visible, float fillAlpha, float lineAlpha)
            {
                _root.SetActive(visible && data != null);
                if (data == null) { _shown = null; return; }
                if (ReferenceEquals(data, _shown)) return;
                _shown = data;
                Build(_fill, data.Quads, false); Build(_line, data.Lines, true);
                _fillMaterial.color = new Color(data.Color.X, data.Color.Y, data.Color.Z, fillAlpha);
                _lineMaterial.color = new Color(data.Color.X, data.Color.Y, data.Color.Z, lineAlpha);
                _fillHost.SetActive(data.Quads.Count > 0); _lineHost.SetActive(data.Lines.Count > 0);
            }
            private static void Build(Mesh mesh, IReadOnlyList<System.Numerics.Vector3> source, bool lines)
            {
                mesh.Clear();
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
            { Destroy(_fill); Destroy(_line); Destroy(_fillMaterial); Destroy(_lineMaterial); }
        }
    }
}
