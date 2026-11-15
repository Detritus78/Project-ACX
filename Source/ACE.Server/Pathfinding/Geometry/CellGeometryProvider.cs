using System;
using System.Collections.Generic;

using DotRecast.Core.Collections;
using DotRecast.Core.Numerics;
using DotRecast.Recast;
using DotRecast.Recast.Geom;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Recast input geometry for one landblock.
    /// Ported from ClassicACE. Project ACX: built straight from the exporter's arrays (ClassicACE wrote an OBJ string
    /// and parsed it back, through a static field that two builds at once would have shared).
    /// </summary>
    public class CellGeometryProvider : IInputGeomProvider
    {
        public readonly float[] vertices;
        public readonly int[] faces;
        public readonly float[] normals;
        private RcVec3f bmin;
        private RcVec3f bmax;
        private readonly List<RcConvexVolume> volumes = new List<RcConvexVolume>();
        private readonly RcTriMesh _mesh;

        /// <summary>
        /// Returns null if the exporter found no triangles
        /// </summary>
        internal static CellGeometryProvider LoadGeometry(LandblockGeometryExporter exporter)
        {
            exporter.GetTriangles(out var vertices, out var faces);

            if (vertices.Length == 0 || faces.Length == 0)
                return null;

            return new CellGeometryProvider(vertices, faces);
        }

        public CellGeometryProvider(float[] vertices, int[] faces)
        {
            this.vertices = vertices;
            this.faces = faces;
            normals = new float[faces.Length];
            CalculateNormals();
            bmin = new RcVec3f(vertices);
            bmax = new RcVec3f(vertices);
            for (int i = 1; i < vertices.Length / 3; i++)
            {
                bmin = RcVec3f.Min(bmin, RcVec.Create(vertices, i * 3));
                bmax = RcVec3f.Max(bmax, RcVec.Create(vertices, i * 3));
            }

            _mesh = new RcTriMesh(vertices, faces);
        }

        public RcTriMesh GetMesh()
        {
            return _mesh;
        }

        public RcVec3f GetMeshBoundsMin()
        {
            return bmin;
        }

        public RcVec3f GetMeshBoundsMax()
        {
            return bmax;
        }

        public IList<RcConvexVolume> ConvexVolumes()
        {
            return volumes;
        }

        public void AddConvexVolume(float[] verts, float minh, float maxh, RcAreaModification areaMod)
        {
            RcConvexVolume vol = new RcConvexVolume();
            vol.hmin = minh;
            vol.hmax = maxh;
            vol.verts = verts;
            vol.areaMod = areaMod;
        }

        public void AddConvexVolume(RcConvexVolume convexVolume)
        {
            volumes.Add(convexVolume);
        }

        public IEnumerable<RcTriMesh> Meshes()
        {
            return RcImmutableArray.Create(_mesh);
        }

        public List<RcOffMeshConnection> GetOffMeshConnections()
        {
            return new List<RcOffMeshConnection>();
        }

        public void AddOffMeshConnection(RcVec3f start, RcVec3f end, float radius, bool bidir, int area, int flags)
        {
        }

        public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter)
        {
        }

        public void CalculateNormals()
        {
            for (int i = 0; i < faces.Length; i += 3)
            {
                int v0 = faces[i] * 3;
                int v1 = faces[i + 1] * 3;
                int v2 = faces[i + 2] * 3;

                var e0 = new RcVec3f();
                var e1 = new RcVec3f();
                e0.X = vertices[v1 + 0] - vertices[v0 + 0];
                e0.Y = vertices[v1 + 1] - vertices[v0 + 1];
                e0.Z = vertices[v1 + 2] - vertices[v0 + 2];

                e1.X = vertices[v2 + 0] - vertices[v0 + 0];
                e1.Y = vertices[v2 + 1] - vertices[v0 + 1];
                e1.Z = vertices[v2 + 2] - vertices[v0 + 2];

                normals[i] = e0.Y * e1.Z - e0.Z * e1.Y;
                normals[i + 1] = e0.Z * e1.X - e0.X * e1.Z;
                normals[i + 2] = e0.X * e1.Y - e0.Y * e1.X;
                float d = (float)Math.Sqrt(normals[i] * normals[i] + normals[i + 1] * normals[i + 1] + normals[i + 2] * normals[i + 2]);
                if (d > 0)
                {
                    d = 1.0f / d;
                    normals[i] *= d;
                    normals[i + 1] *= d;
                    normals[i + 2] *= d;
                }
            }
        }
    }
}
