using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ACE.DatLoader.FileTypes;

using Environment = ACE.DatLoader.FileTypes.Environment;
using Frame = ACE.DatLoader.Entity.Frame;
using Polygon = ACE.DatLoader.Entity.Polygon;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Collects the physics polygons of a set of EnvCells (cell structures plus static objects) as a triangle soup
    /// in Recast's Y-up coordinates.
    /// Ported from ClassicACE. Project ACX changes: vertices are de-duplicated with a dictionary instead of List.IndexOf
    /// (O(n) per vertex), triangles are handed to Recast directly instead of through an OBJ text round trip, and a missing
    /// dat entry skips that piece instead of failing the whole landblock.
    /// </summary>
    internal sealed class LandblockGeometryExporter
    {
        /// <summary>
        /// Environments with outward-facing roof polygons (vaulted ceilings) that would grow navmesh outside the dungeon
        /// </summary>
        private static readonly HashSet<uint> IgnoredEnvironments = new HashSet<uint> { 0x0D0000CA, 0x0D00016D };

        public List<Vector3> Vertices { get; } = new List<Vector3>();

        /// <summary>
        /// Polygons as 0-based indexes into Vertices
        /// </summary>
        public List<List<int>> Polygons { get; } = new List<List<int>>();

        public int SkippedPieces { get; private set; }

        private readonly Dictionary<Vector3, int> vertexIndex = new Dictionary<Vector3, int>();
        private readonly List<EnvCell> cells;
        private readonly DatSource dats;

        public LandblockGeometryExporter(List<EnvCell> cells, DatSource dats)
        {
            this.cells = cells;
            this.dats = dats;
        }

        public void LoadLandblockInfo()
        {
            foreach (var cell in cells)
            {
                LoadEnvironment(cell.EnvironmentId, new List<Frame> { cell.Position }, cell.CellStructure);

                if (cell.StaticObjects != null && cell.StaticObjects.Count > 0)
                {
                    foreach (var staticObject in cell.StaticObjects)
                        LoadSetupOrGfxObj(staticObject.Id, new List<Frame> { staticObject.Frame });
                }
            }
        }

        /// <summary>
        /// Flattens the polygons into the vertex / triangle arrays Recast takes (fan triangulation, as Recast's OBJ importer does)
        /// </summary>
        public void GetTriangles(out float[] vertices, out int[] faces)
        {
            vertices = new float[Vertices.Count * 3];
            for (var i = 0; i < Vertices.Count; i++)
            {
                vertices[i * 3] = Vertices[i].X;
                vertices[i * 3 + 1] = Vertices[i].Y;
                vertices[i * 3 + 2] = Vertices[i].Z;
            }

            var faceList = new List<int>();
            foreach (var polygon in Polygons)
            {
                for (var j = 0; j + 2 < polygon.Count; j++)
                {
                    faceList.Add(polygon[0]);
                    faceList.Add(polygon[j + 1]);
                    faceList.Add(polygon[j + 2]);
                }
            }
            faces = faceList.ToArray();
        }

        private void LoadSetupOrGfxObj(uint id, List<Frame> frames, bool coneTop = false)
        {
            if ((id & 0x02000000) != 0)
                LoadSetup(id, frames, coneTop);
            else
                LoadGfxObj(id, frames);
        }

        private void LoadGfxObj(uint gfxObjId, List<Frame> frames)
        {
            var gfxObj = dats.ReadPortal<GfxObj>(gfxObjId);
            if (gfxObj == null)
            {
                SkippedPieces++;
                return;
            }

            foreach (var poly in gfxObj.PhysicsPolygons.Values)
            {
                var vertices = GetPolygonVertices(poly, gfxObj.VertexArray.Vertices);
                if (vertices != null)
                    AddPolygon(vertices, frames);
            }
        }

        private void LoadSetup(uint setupId, List<Frame> frames, bool coneTop = false)
        {
            var setupModel = dats.ReadPortal<SetupModel>(setupId);
            if (setupModel == null)
            {
                SkippedPieces++;
                return;
            }

            // physics draw priority is child part physics polys, then cylspheres, then spheres?
            var hasPhysicsPolys = false;
            foreach (var partId in setupModel.Parts)
            {
                var gfxObj = dats.ReadPortal<GfxObj>(partId);
                if (gfxObj?.PhysicsPolygons != null && gfxObj.PhysicsPolygons.Count > 0)
                {
                    hasPhysicsPolys = true;
                    break;
                }
            }

            if (setupModel.CylSpheres != null && setupModel.CylSpheres.Count > 0)
            {
                foreach (var cSphere in setupModel.CylSpheres)
                {
                    foreach (var poly in GetCylSpherePolygons(cSphere.Origin, cSphere.Height, cSphere.Radius, coneTop))
                        AddPolygon(poly, frames);
                }
            }

            if (!hasPhysicsPolys && setupModel.Spheres != null && setupModel.Spheres.Count > 0)
            {
                foreach (var sphere in setupModel.Spheres)
                {
                    foreach (var poly in GetSpherePolygons(sphere.Origin, sphere.Radius))
                        AddPolygon(poly, frames);
                }
            }

            // draw all the child parts, always in PlacementFrames[0]
            setupModel.PlacementFrames.TryGetValue(0, out var placement);

            for (var i = 0; i < setupModel.Parts.Count; i++)
            {
                var partFrames = frames.ToList(); // clone
                if (placement != null && i < placement.AnimFrame.Frames.Count)
                    partFrames.Insert(0, placement.AnimFrame.Frames[i]);
                LoadGfxObj(setupModel.Parts[i], partFrames);
            }
        }

        private void LoadEnvironment(uint environmentId, List<Frame> frames, int cellStructure = -1)
        {
            // weird vaulted ceiling environments we dont need... they have outwards facing polys on the roof
            // so we end up generating unneeded navmesh polys "outside" the dungeon if these are left in
            if (IgnoredEnvironments.Contains(environmentId))
                return;

            var environment = dats.ReadPortal<Environment>(environmentId);
            if (environment == null)
            {
                SkippedPieces++;
                return;
            }

            if (cellStructure >= 0 && environment.Cells.TryGetValue((uint)cellStructure, out var cellStruct))
            {
                foreach (var poly in cellStruct.PhysicsPolygons.Values)
                {
                    var vertices = GetPolygonVertices(poly, cellStruct.VertexArray.Vertices);
                    if (vertices != null)
                        AddPolygon(vertices, frames);
                }
            }
            else
            {
                // same fallback ClassicACE's CellGeometry used when the cell structure is missing: take them all
                foreach (var envCellStruct in environment.Cells.Values)
                {
                    foreach (var poly in envCellStruct.PhysicsPolygons.Values)
                    {
                        var vertices = GetPolygonVertices(poly, envCellStruct.VertexArray.Vertices);
                        if (vertices != null)
                            AddPolygon(vertices, frames);
                    }
                }
            }
        }

        private List<Vector3> GetPolygonVertices(Polygon poly, Dictionary<ushort, ACE.DatLoader.Entity.SWVertex> vertexArray)
        {
            var vertices = new List<Vector3>(poly.VertexIds.Count);
            foreach (var vertexId in poly.VertexIds)
            {
                if (!vertexArray.TryGetValue((ushort)vertexId, out var vertex))
                {
                    SkippedPieces++;
                    return null;
                }
                vertices.Add(vertex.Origin);
            }
            return vertices;
        }

        private void AddPolygon(List<Vector3> vertices, List<Frame> frames)
        {
            var poly = new List<int>(vertices.Count);
            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[vertices.Count - 1 - i];
                foreach (var frame in frames)
                {
                    vertex = Transform(vertex, frame.Orientation);
                    vertex += frame.Origin;
                }

                // AC is Z-up, Recast is Y-up
                vertex = new Vector3(vertex.X, vertex.Z, vertex.Y);

                if (!vertexIndex.TryGetValue(vertex, out var index))
                {
                    index = Vertices.Count;
                    Vertices.Add(vertex);
                    vertexIndex.Add(vertex, index);
                }
                poly.Add(index);
            }
            Polygons.Add(poly);
        }

        public static List<List<Vector3>> GetCylSpherePolygons(Vector3 origin, float height, float radius, bool coneTop = false)
        {
            var num_sides = 12;
            var axis = new Vector3(0, 0, height);
            var results = new List<List<Vector3>>();
            // Get two vectors perpendicular to the axis.
            Vector3 v1;
            if (axis.Z < -0.01 || axis.Z > 0.01)
                v1 = new Vector3(axis.Z, axis.Z, -axis.X - axis.Y);
            else
                v1 = new Vector3(-axis.Y - axis.Z, axis.X, axis.X);
            Vector3 v2 = Vector3.Cross(v1, axis);

            // Make the vectors have length radius.
            v1 *= radius / v1.Length();
            v2 *= radius / v2.Length();

            var positions = new List<Vector3>();

            // Make the top end cap.
            // Make the end point.
            int pt0 = positions.Count; // Index of end_point.
            positions.Add(origin);

            // Make the top points.
            double theta = 0;
            double dtheta = 2 * Math.PI / num_sides;
            for (int i = 0; i < num_sides; i++)
            {
                positions.Add(origin +
                    (float)Math.Cos(theta) * v1 +
                    (float)Math.Sin(theta) * v2);
                theta += dtheta;
            }

            // Make the top triangles.
            int pt1 = positions.Count - 1; // Index of last point.
            int pt2 = pt0 + 1;             // Index of first point.
            for (int i = 0; i < num_sides; i++)
            {
                results.Add(new List<Vector3>() { positions[pt0], positions[pt1], positions[pt2] });
                pt1 = pt2++;
            }

            // Make the bottom end cap.
            // Make the end point.
            pt0 = positions.Count; // Index of end_point2.
            Vector3 end_point2 = origin + axis;
            positions.Add(end_point2);

            // Make the bottom points.
            theta = 0;
            for (int i = 0; i < num_sides; i++)
            {
                positions.Add(end_point2 +
                    (float)Math.Cos(theta) * v1 +
                    (float)Math.Sin(theta) * v2);
                theta += dtheta;
            }

            // Make the bottom triangles.
            pt1 = positions.Count - 1; // Index of last point.
            pt2 = pt0 + 1;             // Index of first point.
            var p0 = positions[num_sides + 1];
            if (coneTop && axis.Z > 20)
                p0 += new Vector3(0, 0, 20);

            for (int i = 0; i < num_sides; i++)
            {
                results.Add(new List<Vector3>() { p0, positions[pt2], positions[pt1] });
                pt1 = pt2++;
            }

            // Make the sides.
            // Add the points to the mesh.
            int first_side_point = positions.Count;
            theta = 0;
            for (int i = 0; i < num_sides; i++)
            {
                Vector3 p1 = origin +
                    (float)Math.Cos(theta) * v1 +
                    (float)Math.Sin(theta) * v2;
                positions.Add(p1);
                Vector3 p2 = p1 + axis;
                positions.Add(p2);
                theta += dtheta;
            }

            // Make the side triangles.
            pt1 = positions.Count - 2;
            pt2 = pt1 + 1;
            int pt3 = first_side_point;
            int pt4 = pt3 + 1;
            for (int i = 0; i < num_sides; i++)
            {
                results.Add(new List<Vector3>() { positions[pt1], positions[pt2], positions[pt4] });
                results.Add(new List<Vector3>() { positions[pt1], positions[pt4], positions[pt3] });

                pt1 = pt3;
                pt3 += 2;
                pt2 = pt4;
                pt4 += 2;
            }

            return results;
        }

        public static List<List<Vector3>> GetSpherePolygons(Vector3 origin, float radius)
        {
            var results = new List<List<Vector3>>();
            var vectors = new List<Vector3>();
            var indices = new List<int>();

            Icosahedron(vectors, indices);

            for (var i = 0; i < 1; i++)
                Subdivide(vectors, indices, true);

            // normalize vectors to "inflate" the icosahedron into a sphere.
            for (var i = 0; i < indices.Count / 3; i++)
            {
                var testIndicies = new List<int>();
                for (var x = 2; x >= 0; x--)
                {
                    var ii = i * 3 + x;
                    vectors[indices[ii]] = Vector3.Normalize(vectors[indices[ii]]) * radius;
                    testIndicies.Add(indices[ii]);
                }

                // adjust height..
                // (as in ClassicACE, the sphere's origin is not applied: only its radius and height offset)
                var h = new Vector3(0, 0, radius);
                results.Add(new List<Vector3>() { vectors[testIndicies[0]] + h, vectors[testIndicies[1]] + h, vectors[testIndicies[2]] + h });
            }

            return results;
        }

        public static Vector3 Transform(Vector3 value, Quaternion rotation)
        {
            float x2 = rotation.X + rotation.X;
            float y2 = rotation.Y + rotation.Y;
            float z2 = rotation.Z + rotation.Z;

            float wx2 = rotation.W * x2;
            float wy2 = rotation.W * y2;
            float wz2 = rotation.W * z2;
            float xx2 = rotation.X * x2;
            float xy2 = rotation.X * y2;
            float xz2 = rotation.X * z2;
            float yy2 = rotation.Y * y2;
            float yz2 = rotation.Y * z2;
            float zz2 = rotation.Z * z2;

            return new Vector3(
                value.X * (1.0f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                value.X * (xy2 + wz2) + value.Y * (1.0f - xx2 - zz2) + value.Z * (yz2 - wx2),
                value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1.0f - xx2 - yy2));
        }

        private static int GetMidpointIndex(Dictionary<string, int> midpointIndices, List<Vector3> vertices, int i0, int i1)
        {
            var edgeKey = string.Format("{0}_{1}", Math.Min(i0, i1), Math.Max(i0, i1));

            if (!midpointIndices.TryGetValue(edgeKey, out var midpointIndex))
            {
                var v0 = vertices[i0];
                var v1 = vertices[i1];

                var midpoint = (v0 + v1) / 2f;

                if (vertices.Contains(midpoint))
                    midpointIndex = vertices.IndexOf(midpoint);
                else
                {
                    midpointIndex = vertices.Count;
                    vertices.Add(midpoint);
                    midpointIndices.Add(edgeKey, midpointIndex);
                }
            }

            return midpointIndex;
        }

        /// <remarks>
        ///      i0
        ///     /  \
        ///    m02-m01
        ///   /  \ /  \
        /// i2---m12---i1
        /// </remarks>
        public static void Subdivide(List<Vector3> vectors, List<int> indices, bool removeSourceTriangles)
        {
            var midpointIndices = new Dictionary<string, int>();

            var newIndices = new List<int>(indices.Count * 4);

            if (!removeSourceTriangles)
                newIndices.AddRange(indices);

            for (var i = 0; i < indices.Count - 2; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var m01 = GetMidpointIndex(midpointIndices, vectors, i0, i1);
                var m12 = GetMidpointIndex(midpointIndices, vectors, i1, i2);
                var m02 = GetMidpointIndex(midpointIndices, vectors, i2, i0);

                newIndices.AddRange(new[] {
                    i0, m01, m02,
                    i1, m12, m01,
                    i2, m02, m12,
                    m02, m01, m12
                });
            }

            indices.Clear();
            indices.AddRange(newIndices);
        }

        /// <summary>
        /// create a regular icosahedron (20-sided polyhedron)
        /// </summary>
        /// <remarks>
        /// icosahedron definition may have come from the OpenGL red book.
        /// </remarks>
        public static void Icosahedron(List<Vector3> vertices, List<int> indices)
        {
            indices.AddRange(
                new int[]
                {
                    0,4,1,
                    0,9,4,
                    9,5,4,
                    4,5,8,
                    4,8,1,
                    8,10,1,
                    8,3,10,
                    5,3,8,
                    5,2,3,
                    2,7,3,
                    7,10,3,
                    7,6,10,
                    7,11,6,
                    11,0,6,
                    0,1,6,
                    6,1,10,
                    9,0,11,
                    9,11,2,
                    9,2,5,
                    7,2,11
                }
                .Select(i => i + vertices.Count)
            );

            var X = 0.525731112119133606f;
            var Z = 0.850650808352039932f;

            vertices.AddRange(
                new[]
                {
                    new Vector3(-X, 0f, Z),
                    new Vector3(X, 0f, Z),
                    new Vector3(-X, 0f, -Z),
                    new Vector3(X, 0f, -Z),
                    new Vector3(0f, Z, X),
                    new Vector3(0f, Z, -X),
                    new Vector3(0f, -Z, X),
                    new Vector3(0f, -Z, -X),
                    new Vector3(Z, X, 0f),
                    new Vector3(-Z, X, 0f),
                    new Vector3(Z, -X, 0f),
                    new Vector3(-Z, -X, 0f)
                }
            );
        }
    }
}
