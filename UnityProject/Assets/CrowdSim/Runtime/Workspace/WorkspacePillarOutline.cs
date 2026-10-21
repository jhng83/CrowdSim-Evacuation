using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>One edge mesh per architectural pillar; surface, dimensions and physical model remain independent.</summary>
    public sealed class WorkspacePillarOutline : MonoBehaviour
    {
        private Mesh _mesh;
        public void Initialise(bool circular, Material ink, Material hullInk)
        {
            GameObject hull = new GameObject("Pillar silhouette"); hull.transform.SetParent(transform, false);
            hull.transform.localScale = Vector3.one * 1.008f;
            hull.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
            hull.AddComponent<MeshRenderer>().sharedMaterial = hullInk;
            List<Vector3> points = new List<Vector3>();
            int segments = circular ? 48 : 4;
            float height = circular ? 1 : .5f;
            Vector3 Point(int index, float y)
            {
                if (circular) { float a = index * Mathf.PI * 2 / segments; return new Vector3(Mathf.Cos(a) * .501f, y, Mathf.Sin(a) * .501f); }
                return new Vector3(index == 0 || index == 3 ? -.501f : .501f, y, index < 2 ? -.501f : .501f);
            }
            for (int i = 0; i < segments; i++)
            {
                points.Add(Point(i, height)); points.Add(Point((i + 1) % segments, height));
                points.Add(Point(i, -height)); points.Add(Point((i + 1) % segments, -height));
                if (!circular || i % 12 == 0) { points.Add(Point(i, -height)); points.Add(Point(i, height)); }
            }
            _mesh = new Mesh { name = "Pillar black feature edges" }; _mesh.SetVertices(points);
            int[] indices = new int[points.Count]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            _mesh.SetIndices(indices, MeshTopology.Lines, 0); _mesh.RecalculateBounds();
            GameObject host = new GameObject("Pillar black outline"); host.transform.SetParent(transform, false);
            host.AddComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer renderer = host.AddComponent<MeshRenderer>(); renderer.sharedMaterial = ink;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
