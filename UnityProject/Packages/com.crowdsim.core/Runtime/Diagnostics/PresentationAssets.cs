using System;
using System.Collections.Generic;
using System.Numerics;

namespace CrowdSim.Diagnostics
{
    /// <summary>Deterministic display mesh with the same unit bounds as the planning geometry; never used for physical collision.</summary>
    public sealed class PresentationMesh
    {
        public IReadOnlyList<Vector3> Vertices { get; }
        public IReadOnlyList<Vector3> Normals { get; }
        public IReadOnlyList<Vector2> UV { get; }
        public IReadOnlyList<int> Triangles { get; }
        internal PresentationMesh(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
        { Vertices = vertices.AsReadOnly(); Normals = normals.AsReadOnly(); UV = uv.AsReadOnly(); Triangles = triangles.AsReadOnly(); }
    }
    public static class PresentationAssets
    {
        public static PresentationMesh BevelledBox()
        {
            Vector3[] normals = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
            Vector3[] u = { -Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, -Vector3.UnitX };
            Vector3[] v = { Vector3.UnitY, Vector3.UnitY, -Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY };
            float[] axis = { -.5f, -.44f, .44f, .5f };
            List<Vector3> vertices = new List<Vector3>(), directions = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>(); List<int> triangles = new List<int>();
            for (int face = 0; face < 6; face++)
            {
                int offset = vertices.Count;
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    Vector3 p = normals[face] * .5f + u[face] * axis[x] + v[face] * axis[y];
                    Vector3 core = Vector3.Clamp(p, new Vector3(-.44f), new Vector3(.44f));
                    Vector3 normal = Vector3.Normalize(p - core);
                    vertices.Add(core + normal * .06f); directions.Add(normal); uv.Add(new Vector2(axis[x] + .5f, axis[y] + .5f));
                }
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int a = offset + y * 4 + x;
                    triangles.AddRange(new[] { a, a + 1, a + 5, a, a + 5, a + 4 });
                }
            }
            return new PresentationMesh(vertices, directions, uv, triangles);
        }
    }
}
