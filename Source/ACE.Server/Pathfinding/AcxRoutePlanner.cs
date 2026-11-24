using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;

using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity;

using DotRecast.Core.Numerics;
using DotRecast.Detour;

using log4net;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Project ACX (2026-11-22): a walking route through a whole dungeon for UCM, that never comes near a hotspot (JJ, for
    /// Matron Hive South: "avoid ANY acid. Don't cross it don't touch it"). Admin console only; a planning tool, nothing in
    /// game uses it. On its own copy of the landblock's narrow mesh it shuts every polygon that comes within `clearance`
    /// metres (across) of a hotspot whose height overlaps it, then walks the dungeon's cells in depth-first order from the
    /// start cell, routing to each cell's middle on what is left. Cells already passed within 6 m are skipped. Writes
    /// Pathfinding/route-XXXX.json: the route's corners, the hotspots, and what was skipped. Claude's ucm_route.py checks it
    /// against the hotspots again and turns it into a UCM routes profile.
    /// </summary>
    public static class AcxRoutePlanner
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>how far routes keep from a portal, across</summary>
        public const float PortalClearance = 4f;

        public class Hotspot { public uint Wcid { get; set; } public string Name { get; set; } public uint Cell { get; set; } public float X { get; set; } public float Y { get; set; } public float Z { get; set; } }
        public class Output
        {
            public string Landblock { get; set; }
            public float Clearance { get; set; }
            public int ClosedPolys { get; set; }
            public int Cells { get; set; }
            public int ShutDoorways { get; set; }
            public int WideLegs { get; set; }
            public int EasedCorners { get; set; }
            public int NarrowLegs { get; set; }
            public int Doors { get; set; }
            public List<float[]> Route { get; set; } = new();
            public List<Hotspot> Hotspots { get; set; } = new();
            public List<Hotspot> Portals { get; set; } = new();
            public List<string> Unreached { get; set; } = new();
        }

        public static string Start(ushort landblock, uint startCell, float clearance, bool skipDoors = false, float cover = 12f, Position from = null, Position to = null)
        {
            CoverRadius = cover;
            Task.Run(() =>
            {
                try { log.Info($"[Route] {Plan(landblock, startCell, clearance, skipDoors, from, to)}"); }
                catch (Exception e) { log.Error($"[Route] {landblock:X4} failed: {e}"); }
            });
            return $"Planning a route through {landblock:X4} from {startCell:X8}, {clearance} m from hotspots{(skipDoors ? ", not through doors" : "")}. Result goes to the server log.";
        }

        private static string Plan(ushort landblock, uint startCell, float clearance, bool skipDoors, Position fromSpot = null, Position toSpot = null)
        {
            // routes go on the wide mesh where they can (1.4 m off the walls, so a melee character darting back to the route
            // doesn't catch a corner; JJ 2026-11-23: "your corners are likely to close to the wall"), on the narrow one where
            // a passage is too tight for it
            if (!Pathfinder.TryReadMeshes(landblock, out var mesh, out var wide) || mesh == null)
                return $"{landblock:X4}: no mesh";

            var output = new Output { Landblock = $"{landblock:X4}", Clearance = clearance };

            // the hotspots (acid, fire and the like), and the doors
            var doors = new List<Vector3>();
            foreach (var inst in DatabaseManager.World.GetCachedInstancesByLandblock(landblock))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(inst.WeenieClassId);
                if (weenie?.WeenieType == WeenieType.Door)
                    doors.Add(new Vector3(inst.OriginX, inst.OriginY, inst.OriginZ));
                // portals too, closer: walking into one takes you out of the dungeon (JJ 2026-11-23: "this runs us right
                // into a exit portal")
                if (weenie?.WeenieType == WeenieType.Portal)
                {
                    weenie.PropertiesString.TryGetValue(ACE.Entity.Enum.Properties.PropertyString.Name, out var portalName);
                    output.Portals.Add(new Hotspot { Wcid = inst.WeenieClassId, Name = portalName, Cell = inst.ObjCellId, X = inst.OriginX, Y = inst.OriginY, Z = inst.OriginZ });
                }
                if (weenie == null || weenie.WeenieType != WeenieType.HotSpot)
                    continue;
                weenie.PropertiesString.TryGetValue(ACE.Entity.Enum.Properties.PropertyString.Name, out var name);
                output.Hotspots.Add(new Hotspot { Wcid = inst.WeenieClassId, Name = name, Cell = inst.ObjCellId, X = inst.OriginX, Y = inst.OriginY, Z = inst.OriginZ });
            }

            // shut the polygons near them, on both meshes
            output.ClosedPolys = CloseNear(mesh, output.Hotspots, clearance) + (wide != null ? CloseNear(wide, output.Hotspots, clearance) : 0)
                + CloseNear(mesh, output.Portals, PortalClearance) + (wide != null ? CloseNear(wide, output.Portals, PortalClearance) : 0);

            // Project ACX: a straight run from one spot to another, the shortest open way (JJ 2026-11-24, Tusker Abode: "just
            // navigates the main big hall not the small halls. It should go from the drop point all the way to the jump down
            // part stop and turn around"); UCM's reverse_route walks it back
            if (fromSpot != null && toSpot != null)
            {
                var a = ClosestOpen(wide ?? mesh, fromSpot) ?? ClosestOpen(mesh, fromSpot);
                var b = ClosestOpen(wide ?? mesh, toSpot) ?? ClosestOpen(mesh, toSpot);
                if (a == null || b == null)
                    return $"{landblock:X4}: start or end is off the open mesh";
                var line = wide != null ? Pathfinder.FindRoute(wide, a, b) : null;
                if (line == null || line.Count == 0 || Vector3.Distance(line[^1].Pos, b.Pos) > 1.5f)
                {
                    line = Pathfinder.FindRoute(mesh, a, b);
                    if (line == null || line.Count == 0 || Vector3.Distance(line[^1].Pos, b.Pos) > 1.5f)
                        return $"{landblock:X4}: no open way from start to end";
                    output.NarrowLegs++;
                }
                else
                    output.WideLegs++;
                line = EaseCorners(mesh, line, 2.5f, out var easedLine);
                output.EasedCorners = easedLine;
                output.Route = line.Select(r => new[] { r.PositionX, r.PositionY, r.PositionZ }).ToList();
                var linePath = Path.Combine(Pathfinder.RootDirectory, $"route-{landblock:X4}.json");
                File.WriteAllText(linePath, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                return $"{landblock:X4}: {line.Count} points from start to end, {output.ClosedPolys} polygons shut; written to {linePath}";
            }

            // the cells and their doorways
            var info = DatManager.CellDat.ReadFromDat<LandblockInfo>((uint)landblock << 16 | 0xFFFE);
            var cells = new Dictionary<uint, EnvCell>();
            for (var i = 0u; i < info.NumCells; i++)
            {
                var id = (uint)landblock << 16 | (0x100 + i);
                try { cells[id] = DatManager.CellDat.ReadFromDat<EnvCell>(id); } catch { }
            }
            output.Cells = cells.Count;
            output.Doors = doors.Count;

            // with skipDoors, a doorway with a door in it (within 1.5 m of its middle across; a door stands on the floor, the middle is higher) is a wall: the route stays in the halls
            // and leaves the side rooms behind doors alone (JJ: "theres no reason to open all those little doors. They are
            // coves where spawns happen then they run out after the player")
            var shut = new HashSet<(uint, uint)>();
            if (skipDoors)
            {
                foreach (var (id, cell) in cells)
                {
                    var env = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(0x0D000000 | cell.EnvironmentId);
                    if (env == null || !env.Cells.TryGetValue(cell.CellStructure, out var cs))
                        continue;
                    foreach (var portal in cell.CellPortals)
                    {
                        if (!cs.Polygons.TryGetValue(portal.PolygonId, out var poly))
                            continue;
                        var mid = Vector3.Zero;
                        foreach (var v in poly.VertexIds) mid += cs.VertexArray.Vertices[(ushort)v].Origin;
                        mid = Vector3.Transform(mid / poly.VertexIds.Count, cell.Position.Orientation) + cell.Position.Origin;
                        if (doors.Any(d => Vector2.Distance(new Vector2(d.X, d.Y), new Vector2(mid.X, mid.Y)) < 1.5f && Math.Abs(d.Z - mid.Z) < 4f))
                        {
                            var other = (uint)landblock << 16 | portal.OtherCellId;
                            shut.Add((id, other)); shut.Add((other, id));
                        }
                    }
                }
                output.ShutDoorways = shut.Count / 2;
            }

            // depth first from the start cell
            var order = new List<uint>();
            var seen = new HashSet<uint>();
            var stack = new Stack<uint>();
            stack.Push(startCell);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!seen.Add(id) || !cells.TryGetValue(id, out var cell))
                    continue;
                order.Add(id);
                foreach (var portal in cell.CellPortals.AsEnumerable().Reverse())
                {
                    var other = (uint)landblock << 16 | portal.OtherCellId;
                    if (!shut.Contains((id, other)))
                        stack.Push(other);
                }
            }

            Position here = null;
            var route = new List<Position>();
            // a cell counts as covered when the route passes within CoverRadius of it in a straight open line: walking past
            // a room's doorway pulls its monsters, so the route needn't sweep every room (JJ 2026-11-23: "Theres no reason to
            // run every inch of a dungeon. You want a main path that covers the dungeon ... If you walk into a room you aggro
            // the mobs so you dont need to walk the entire room")
            var trail = new List<Vector3>();
            var coverQuery = new DtNavMeshQuery(mesh);
            var coverFilter = new DtQueryDefaultFilter();
            foreach (var id in order)
            {
                var o = cells[id].Position.Origin;
                var target = new Position(id, new Vector3(o.X, o.Y, o.Z), Quaternion.Identity);
                var onWide = wide != null ? Near(ClosestOpen(wide, target), o) : null;
                var onMesh = onWide ?? Near(ClosestOpen(mesh, target), o);
                if (onMesh == null)
                {
                    output.Unreached.Add($"{id:X8} off the open mesh at {o.X:0} {o.Y:0} {o.Z:0}");
                    continue;
                }
                if (here == null)
                {
                    here = onMesh;
                    route.Add(here);
                    trail.Add(here.Pos);
                    continue;
                }
                if (Covered(coverQuery, coverFilter, trail, onMesh.Pos))
                    continue;   // the route already passes in sight of it
                var leg = onWide != null ? Pathfinder.FindRoute(wide, here, onWide) : null;
                if (leg == null || leg.Count == 0 || Vector3.Distance(leg[^1].Pos, onMesh.Pos) > 1.5f)
                {
                    leg = Pathfinder.FindRoute(mesh, here, onMesh);
                    if (leg == null || leg.Count == 0 || Vector3.Distance(leg[^1].Pos, onMesh.Pos) > 1.5f)
                    {
                        output.Unreached.Add($"{id:X8} no open way at {o.X:0} {o.Y:0} {o.Z:0}");
                        continue;
                    }
                    output.NarrowLegs++;
                }
                else
                    output.WideLegs++;
                foreach (var next in leg.Skip(1))
                {
                    var from = route[^1].Pos;
                    var n = Math.Max(1, (int)(Vector3.Distance(from, next.Pos) / 2f));
                    for (var k = 1; k <= n; k++)
                        trail.Add(Vector3.Lerp(from, next.Pos, k / (float)n));
                    route.Add(next);
                }
                here = leg[^1];
            }

            route = EaseCorners(mesh, route, 2.5f, out var eased);
            output.EasedCorners = eased;
            output.Route = route.Select(r => new[] { r.PositionX, r.PositionY, r.PositionZ }).ToList();
            var path = Path.Combine(Pathfinder.RootDirectory, $"route-{landblock:X4}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            return $"{landblock:X4}: {route.Count} points, {output.Hotspots.Count} hotspots, {output.ClosedPolys} polygons shut, {output.WideLegs} wide / {output.NarrowLegs} narrow legs, {output.EasedCorners} corners eased off walls, {output.Unreached.Count} of {order.Count} cells skipped; written to {path}";
        }


        /// <summary>shuts every polygon within `clearance` (across) of a hotspot at about its height. Recast's frame is (x, height, y)</summary>
        private static int CloseNear(DtNavMesh mesh, List<Hotspot> hotspots, float clearance)
        {
            var closed = 0;
            for (var t = 0; t < mesh.GetMaxTiles(); t++)
            {
                var tile = mesh.GetTile(t);
                if (tile?.data?.header == null)
                    continue;
                var baseRef = mesh.GetPolyRefBase(tile);
                for (var p = 0; p < tile.data.header.polyCount; p++)
                {
                    var poly = tile.data.polys[p];
                    var pts = new List<Vector3>();
                    for (var v = 0; v < poly.vertCount; v++)
                    {
                        var i = poly.verts[v] * 3;
                        pts.Add(new Vector3(tile.data.verts[i], tile.data.verts[i + 2], tile.data.verts[i + 1]));   // back to (x, y, z)
                    }
                    var zMin = pts.Min(q => q.Z); var zMax = pts.Max(q => q.Z);
                    // a hotspot's cylinder stands about 1.5 m tall; a walker is about 2 m. Take 3 m either way to be sure
                    if (hotspots.Any(h => zMax > h.Z - 3f && zMin < h.Z + 3f && Distance2D(pts, new Vector2(h.X, h.Y)) < clearance))
                    {
                        mesh.SetPolyFlags(baseRef | (long)p, 0);   // the default filter passes no polygon with no flags
                        closed++;
                    }
                }
            }

            return closed;
        }


        /// <summary>
        /// Moves each turn of the route out from the walls, to `want` metres where the passage is wide enough and toward its
        /// middle where it isn't (JJ 2026-11-23, a turn jammed into a corner: "see your turn waypoint"). A path's corners lie
        /// on the mesh's edge, as tight to the wall as the mesh lets them. A turn only moves if both legs to it stay on the
        /// open mesh (no shortcut through a wall, or onto the shut ground round a hotspot).
        /// </summary>
        private static List<Position> EaseCorners(DtNavMesh mesh, List<Position> route, float want, out int eased)
        {
            eased = 0;
            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();
            var outRoute = new List<Position>(route);
            for (var i = 1; i < outRoute.Count - 1; i++)
            {
                var p = new RcVec3f(outRoute[i].PositionX, outRoute[i].PositionZ, outRoute[i].PositionY);
                for (var step = 0; step < 8; step++)
                {
                    query.FindNearestPoly(p, new RcVec3f(1f, 2f, 1f), filter, out long polyRef, out var onPoly, out _);
                    if (polyRef == 0)
                        break;
                    p = onPoly;
                    query.FindDistanceToWall(polyRef, p, want, filter, out var dist, out _, out var normal);
                    if (dist >= want - 0.05f)
                        break;
                    var move = (want - dist) * 0.6f;
                    p = new RcVec3f(p.X + normal.X * move, p.Y, p.Z + normal.Z * move);
                }
                var moved = new Position(outRoute[i].Cell, new Vector3(p.X, p.Z, p.Y), Quaternion.Identity);
                if (Vector3.Distance(moved.Pos, outRoute[i].Pos) < 0.1f)
                    continue;
                if (OnMeshAlong(query, filter, outRoute[i - 1].Pos, moved.Pos) && OnMeshAlong(query, filter, moved.Pos, outRoute[i + 1].Pos))
                {
                    outRoute[i] = moved;
                    eased++;
                }
            }
            return outRoute;
        }


        /// <summary>how near (and in a straight open line) the route must pass for a part of the dungeon to count as covered</summary>
        public static float CoverRadius = 12f;   // deep enough into a room that its monsters reach you before you turn back (JJ: "walk deep enough into a room so that you don't just turn around instantly")

        private static bool Covered(DtNavMeshQuery query, IDtQueryFilter filter, List<Vector3> trail, Vector3 spot)
        {
            foreach (var t in trail.Where(t => Math.Abs(t.Z - spot.Z) < 4f && Vector3.Distance(t, spot) < CoverRadius).OrderBy(t => Vector3.Distance(t, spot)).Take(12))
                if (OnMeshAlong(query, filter, t, spot))
                    return true;
            return false;
        }

        /// <summary>whether a straight line stays on the open mesh (checked every 0.25 m)</summary>
        private static bool OnMeshAlong(DtNavMeshQuery query, IDtQueryFilter filter, Vector3 a, Vector3 b)
        {
            var n = Math.Max(1, (int)(Vector3.Distance(a, b) / 0.25f));
            for (var k = 0; k <= n; k++)
            {
                var q = Vector3.Lerp(a, b, k / (float)n);
                query.FindNearestPoly(new RcVec3f(q.X, q.Z, q.Y), new RcVec3f(0.3f, 2f, 0.3f), filter, out long r, out var pt, out _);
                if (r == 0 || Vector2.Distance(new Vector2(pt.X, pt.Z), new Vector2(q.X, q.Y)) > 0.05f)
                    return false;
            }
            return true;
        }

        private static Position Near(Position p, System.Numerics.Vector3 o) => p != null && Vector2.Distance(new Vector2(p.PositionX, p.PositionY), new Vector2(o.X, o.Y)) <= 4f ? p : null;

        private static Position ClosestOpen(DtNavMesh mesh, Position pos)
        {
            var query = new DtNavMeshQuery(mesh);
            var status = query.FindNearestPoly(new RcVec3f(pos.PositionX, pos.PositionZ, pos.PositionY), new RcVec3f(4f, 6f, 4f), new DtQueryDefaultFilter(), out long polyRef, out var pt, out _);
            if (status.Failed() || polyRef == 0)
                return null;
            return new Position(pos.Cell, new Vector3(pt.X, pt.Z, pt.Y), Quaternion.Identity);
        }

        /// <summary>how far a point is from a polygon, across (0 inside)</summary>
        private static float Distance2D(List<Vector3> poly, Vector2 p)
        {
            var inside = false;
            var best = float.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = new Vector2(poly[i].X, poly[i].Y); var b = new Vector2(poly[j].X, poly[j].Y);
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
                var ab = b - a;
                var t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                best = Math.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return inside ? 0f : best;
        }
    }
}
