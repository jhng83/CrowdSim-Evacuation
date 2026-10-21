using System;
using CrowdSim.Diagnostics;
using UnityEngine;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Owned, procedural presentation assets; original dimensions and detached model anchors remain authoritative.</summary>
    internal sealed class WorkspaceSurfaceLibrary : IDisposable
    {
        public Mesh Box { get; }
        public Texture2D Wood { get; }
        public Texture2D Floor { get; }
        public Texture2D Barrier { get; }
        public WorkspaceSurfaceLibrary()
        {
            PresentationMesh data = PresentationAssets.BevelledBox();
            Vector3[] vertices = new Vector3[data.Vertices.Count], normals = new Vector3[data.Normals.Count];
            Vector2[] uv = new Vector2[data.UV.Count]; int[] triangles = new int[data.Triangles.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(data.Vertices[i].X, data.Vertices[i].Y, data.Vertices[i].Z);
                normals[i] = new Vector3(data.Normals[i].X, data.Normals[i].Y, data.Normals[i].Z);
                uv[i] = new Vector2(data.UV[i].X, data.UV[i].Y);
            }
            for (int i = 0; i < triangles.Length; i++) triangles[i] = data.Triangles[i];
            Box = new Mesh { name = "CrowdSim / chamfered box", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            Box.RecalculateBounds();
            Wood = Pattern("Warm ash grain", 0); Floor = Pattern("Slate tile", 1); Barrier = Pattern("Safety stripe", 2);
        }
        private static Texture2D Pattern(string name, int mode)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = .94f + .04f * Mathf.Sin(y * .7f + Mathf.Sin(x * .14f));
                if (mode == 1) grain = x < 2 || y < 2 ? .84f : .98f;
                colors[y * size + x] = mode == 2 ? ((x + y) / 12 % 2 == 0 ? new Color(.95f, .78f, .36f) : new Color(.24f, .29f, .33f)) : new Color(grain, grain, grain);
            }
            texture.SetPixels(colors); texture.Apply(true, true); return texture;
        }
        public void Dispose()
        { UnityEngine.Object.Destroy(Box); UnityEngine.Object.Destroy(Wood); UnityEngine.Object.Destroy(Floor); UnityEngine.Object.Destroy(Barrier); }
    }
}
