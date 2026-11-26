using UnityEngine;

namespace CostaLivre
{
    internal static class CostaMesh
    {
        internal readonly struct Section
        {
            internal readonly float Z;
            internal readonly float Width;
            internal readonly float Bottom;
            internal readonly float Top;

            internal Section(float z, float width, float bottom, float top)
            {
                Z = z;
                Width = width;
                Bottom = bottom;
                Top = top;
            }
        }

        internal static Mesh Loft(string name, params Section[] sections)
        {
            const int sides = 8, stride = sides + 1;
            Vector3[] vertices = new Vector3[sections.Length * stride];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] indices = new int[(sections.Length - 1) * sides * 6 + (sides - 2) * 6];

            for (int i = 0; i < sections.Length; i++)
            {
                Section ring = sections[i];
                float bevel = 0.26f * Mathf.Min(ring.Width, ring.Top - ring.Bottom);
                int v = i * stride;
                vertices[v] = new(-ring.Width + bevel, ring.Bottom, ring.Z);
                vertices[v + 1] = new(ring.Width - bevel, ring.Bottom, ring.Z);
                vertices[v + 2] = new(ring.Width, ring.Bottom + bevel, ring.Z);
                vertices[v + 3] = new(ring.Width, ring.Top - bevel, ring.Z);
                vertices[v + 4] = new(ring.Width - bevel, ring.Top, ring.Z);
                vertices[v + 5] = new(-ring.Width + bevel, ring.Top, ring.Z);
                vertices[v + 6] = new(-ring.Width, ring.Top - bevel, ring.Z);
                vertices[v + 7] = new(-ring.Width, ring.Bottom + bevel, ring.Z);
                vertices[v + sides] = vertices[v];
                for (int side = 0; side <= sides; side++)
                    uv[v + side] = new Vector2(ring.Z * 0.33f, (float)side / sides);
            }

            int face = 0;
            for (int i = 0; i < sections.Length - 1; i++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int a = i * stride + side;
                    int b = a + 1;
                    indices[face++] = a;
                    indices[face++] = b;
                    indices[face++] = a + stride;
                    indices[face++] = b;
                    indices[face++] = b + stride;
                    indices[face++] = a + stride;
                }
            }

            int back = vertices.Length - stride;
            for (int i = 1; i < sides - 1; i++)
            {
                indices[face++] = 0;
                indices[face++] = i + 1;
                indices[face++] = i;
                indices[face++] = back;
                indices[face++] = back + i;
                indices[face++] = back + i + 1;
            }

            Mesh mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            for (int ring = 0; ring < sections.Length; ring++)
            {
                int first = ring * stride;
                normals[first] = normals[first + sides] = (normals[first] + normals[first + sides]).normalized;
            }
            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
