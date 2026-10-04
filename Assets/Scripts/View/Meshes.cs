using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastLight.View
{
    /// <summary>Procedural meshes: quads for glows and decals, the sea grid, simple extrusions.</summary>
    public static class Meshes
    {
        static Mesh quadXY, quadXZ;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { quadXY = null; quadXZ = null; }

        /// <summary>Quad in the XY plane, corners at +-0.5 (billboards).</summary>
        public static Mesh QuadXY
        {
            get
            {
                if (quadXY != null) return quadXY;
                quadXY = new Mesh { name = "LL_QuadXY" };
                quadXY.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
                quadXY.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quadXY.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quadXY.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                return quadXY;
            }
        }

        /// <summary>Flat quad in the XZ plane, corners at +-0.5, UV v along +z (north).</summary>
        public static Mesh QuadXZ
        {
            get
            {
                if (quadXZ != null) return quadXZ;
                quadXZ = new Mesh { name = "LL_QuadXZ" };
                quadXZ.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
                quadXZ.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quadXZ.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                quadXZ.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                return quadXZ;
            }
        }

        /// <summary>
        /// The sea: dense near the play area, stretching to the horizon with growing spacing so the
        /// swell can displace it where it matters.
        /// </summary>
        public static Mesh SeaGrid(Rect area, float cell, float farExtent)
        {
            var xs = new List<float>();
            var zs = new List<float>();
            BuildAxis(xs, area.xMin, area.xMax, cell, farExtent);
            BuildAxis(zs, area.yMin, area.yMax, cell, farExtent);
            int nx = xs.Count, nz = zs.Count;
            var verts = new Vector3[nx * nz];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                    verts[z * nx + x] = new Vector3(xs[x], 0, zs[z]);
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int z = 0; z < nz - 1; z++)
                for (int x = 0; x < nx - 1; x++)
                {
                    int i = z * nx + x;
                    tris[t++] = i; tris[t++] = i + nx; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + nx; tris[t++] = i + nx + 1;
                }
            var mesh = new Mesh { name = "LL_Sea", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(farExtent * 4f, 10f, farExtent * 4f));
            return mesh;
        }

        static void BuildAxis(List<float> list, float min, float max, float cell, float far)
        {
            // Outward from the dense core with spacing growing geometrically.
            var left = new List<float>();
            float step = cell, p = min;
            while (p > -far) { step *= 1.18f; p -= step; left.Add(p); }
            left.Reverse();
            list.AddRange(left);
            for (float v = min; v <= max + 1e-3f; v += cell) list.Add(v);
            step = cell;
            p = list[list.Count - 1];
            while (p < far) { step *= 1.18f; p += step; list.Add(p); }
        }

        /// <summary>Extruded polygon (x, z points) from y0 to y1, with a cap on top. Flat-shaded walls.</summary>
        public static Mesh Extrude(Vector2[] poly, float y0, float y1, Color top, Color side)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            bool ccw = SignedArea(poly) > 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                int baseIndex = verts.Count;
                verts.Add(new Vector3(a.x, y0, a.y));
                verts.Add(new Vector3(b.x, y0, b.y));
                verts.Add(new Vector3(b.x, y1, b.y));
                verts.Add(new Vector3(a.x, y1, a.y));
                for (int k = 0; k < 4; k++) cols.Add(side);
                if (ccw) tris.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1, baseIndex, baseIndex + 3, baseIndex + 2 });
                else tris.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 });
            }
            int capStart = verts.Count;
            foreach (var p in poly) { verts.Add(new Vector3(p.x, y1, p.y)); cols.Add(top); }
            var capTris = Triangulate(poly);
            for (int i = 0; i < capTris.Count; i += 3)
            {
                // Triangulate always returns counter-clockwise triangles (seen from above); Unity's
                // front faces are clockwise, so flip them for an upward-facing cap.
                tris.Add(capStart + capTris[i]); tris.Add(capStart + capTris[i + 2]); tris.Add(capStart + capTris[i + 1]);
            }
            var mesh = new Mesh { name = "LL_Extrude", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        public static float SignedArea(Vector2[] poly)
        {
            float a = 0f;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                a += (poly[j].x * poly[i].y) - (poly[i].x * poly[j].y);
            return a * 0.5f;
        }

        /// <summary>Ear-clipping triangulation of a simple polygon. Returns index triples (CCW).</summary>
        public static List<int> Triangulate(Vector2[] poly)
        {
            var result = new List<int>();
            int n = poly.Length;
            if (n < 3) return result;
            var idx = new List<int>(n);
            bool ccw = SignedArea(poly) > 0f;
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    var a = poly[i0]; var b = poly[i1]; var c = poly[i2];
                    if (Cross(b - a, c - b) <= 0f) continue;
                    bool inside = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int ik = idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (InTriangle(poly[ik], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(i0); result.Add(i1); result.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }

        /// <summary>Open cone along +Z from the apex, for the near-field beam core.</summary>
        public static Mesh Cone(float length, float radius, int segments)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length);
                verts.Add(Vector3.zero); uvs.Add(new Vector2(i / (float)segments, 0));
                verts.Add(ring); uvs.Add(new Vector2(i / (float)segments, 1));
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "LL_Cone" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
