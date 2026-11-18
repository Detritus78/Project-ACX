using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: one portal of every look in the world database, set out in rows in front of the admin (JJ 2026-11-18:
    /// "I want to a see sample of every portal style available"). A look is a model (Setup) and color (PaletteTemplate);
    /// the wcid is the lowest one with that look. Rarest first (JJ: "the first ones ... are portals that are not in service"): never in the
    /// game, then only from spawners, then by how many are placed in the world (landblock_instance). They keep their names with a number in
    /// front, still work as portals, and vanish after an hour (or on restart).
    /// </summary>
    public static class ACXPortalGalleryCommands
    {
        private static readonly uint[] Looks =
        {
            3657, 71866, 14583, 72175,   // 1-4: never in the game (not placed, not spawned)
            33185, 1955, 14581, 14582, 52593,   // 5-9: only from spawners
            34513, 28096, 25392, 34515, 34516, 34831, 34510, 29958, 33302, 14579, 34511, 36769, 43865, 46590, 34512,   // 10-24: placed once
            34272, 7289, 6634, 34829, 32811, 37051, 29778, 28478, 8106, 85002, 8906, 33295, 14584, 34771, 28981, 7413, 42956, 24038, 24037, 3957, 34828, 22927, 34767, 1127, 24003, 3628, 1327, 1013, 428, 1311, 14603, 368   // the rest, rarest to most common
        };

        [CommandHandler("acxportals", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Sets out one portal of every look in rows in front of you (8 per row, 5 m apart); they vanish after an hour.")]
        public static void HandleAcxPortals(Session session, params string[] parameters)
        {
            var player = session.Player;
            var at = player.Location;
            var heading = Math.Atan2(2 * at.RotationW * at.RotationZ, 1 - 2 * at.RotationZ * at.RotationZ);
            var forward = new Vector2(-(float)Math.Sin(heading), (float)Math.Cos(heading));
            var right = new Vector2(forward.Y, -forward.X);
            var made = 0;
            for (var i = 0; i < Looks.Length; i++)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(Looks[i]);
                if (wo == null) continue;
                int row = i / 8, column = i % 8;
                var offset = forward * (8 + row * 6) + right * ((column - 3.5f) * 5);
                var spot = at.InFrontOf(0, true);   // facing back toward the player
                spot.PositionX = at.PositionX + offset.X;
                spot.PositionY = at.PositionY + offset.Y;
                spot.PositionZ = at.PositionZ + 0.05f;
                spot.LandblockId = new LandblockId(spot.GetCell());
                if (at.Indoors == false)
                    spot.AdjustMapCoords();
                wo.Location = spot;
                wo.Name = $"{i + 1}. {wo.Name}";
                wo.Lifespan = 3600;
                if (wo.EnterWorld()) made++;
            }
            CommandHandlerHelper.WriteOutputInfo(session, $"Set out {made} of {Looks.Length} portal looks: rows of 8 going away from you, numbered left to right. They vanish in an hour.");
        }

        // pictures from the particle effects that can be painted on a picture card like #30's (South Gate) disc: the
        // vortex swirls in each color, the glowing orbs, and the disc's own pink cloud (havencells glowtex, 2026-11-18)
        private static readonly Dictionary<string, uint> Pictures = new(StringComparer.OrdinalIgnoreCase)
        {
            ["pink-cloud"] = 0x05001BC9,
            ["yellow"] = 0x05001768, ["green"] = 0x0500176A, ["red"] = 0x0500176C, ["purple"] = 0x0500176D, ["emerald"] = 0x0500176F,
            ["orange"] = 0x05001770, ["gold"] = 0x05001771, ["pink"] = 0x05001772, ["amber"] = 0x05001774, ["blue"] = 0x0500180E,
            ["white"] = 0x0500186D, ["silver"] = 0x0500186E, ["violet"] = 0x050016E7,
            ["orb-blue"] = 0x0500182D, ["orb-cyan"] = 0x0500183B, ["orb-green"] = 0x0500183C, ["orb-crimson"] = 0x0500183D, ["orb-red"] = 0x0500183E,
        };

        /// <summary>
        /// JJ 2026-11-18: "take two of those and put one 90deg to the other in the center" (the Concordia floor portal: the
        /// Portalling Device frame with a nether hub and the red spark, 30-hub-portal.sql + 95-portal-looks.sql). A mock-up:
        /// two of them crossed, with their sparks, 5 m in front; replaces the last mix. They vanish after an hour.
        /// </summary>
        [CommandHandler("acxportalcross", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Mock-up: two Concordia floor portals crossed at 90 degrees, 5 m in front of you; replaces your last mix. horns: the second one is only its horns (JJ: \"just take the left and right horns and rotate them and they both use the same middle ball\").", "[spark size, default 0.5] [horns]")]
        public static void HandleAcxPortalCross(Session session, params string[] parameters)
        {
            var player = session.Player;
            var spark = parameters.Length > 0 && float.TryParse(parameters[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var sp) ? sp : 0.5f;
            var hornsOnly = Array.Exists(parameters, p => p.Equals("horns", StringComparison.OrdinalIgnoreCase));
            if (mixes.TryRemove(player.Guid.Full, out var old))
                foreach (var o in old) o.Destroy();

            var spot = player.Location.InFrontOf(5, true);
            spot.LandblockId = new LandblockId(spot.GetCell());
            if (player.Location.Indoors == false)
                spot.AdjustMapCoords();

            var made = new List<WorldObject>();
            foreach (var turn in new[] { 0f, MathF.PI / 2 })
            {
                var at = new Position(spot);
                at.Rotation = at.Rotation * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, turn);
                var frame = WorldObjectFactory.CreateNewWorldObject(900728);
                var card = WorldObjectFactory.CreateNewWorldObject(901005);
                if (frame == null || card == null) return;
                var cardAt = new Position(at);
                cardAt.PositionZ += 2.29f * (frame.ObjScale ?? 1);
                frame.Location = at;
                frame.Name = "Crossed portal (mock-up)";
                frame.Lifespan = 3600;
                card.Location = cardAt;
                card.ObjScale = spark;
                card.Lifespan = 3600;
                if (hornsOnly && turn > 0)
                {
                    // the ball (part 2) and arms (3, 4) painted with the regular portal card's see-through picture
                    frame.SetAcxTextureSwap(2, 0x05001773, 0x0500264C);
                    frame.SetAcxTextureSwap(3, 0x050020B0, 0x0500264C);
                    frame.SetAcxTextureSwap(4, 0x050020B0, 0x0500264C);
                    frame.EnterWorld();
                    made.Add(frame);
                    continue;
                }
                frame.EnterWorld();
                card.EnterWorld();
                made.Add(frame);
                made.Add(card);
            }
            mixes[player.Guid.Full] = made;
            CommandHandlerHelper.WriteOutputInfo(session, $"Crossed portals 5 m in front of you, sparks at size {spark.ToString(CultureInfo.InvariantCulture)}. They vanish in an hour.");
        }

        // each admin's last mix, replaced by the next one
        private static readonly ConcurrentDictionary<uint, List<WorldObject>> mixes = new();

        /// <summary>
        /// JJ 2026-11-18: "I really like #17 as the base and maybe we can shrink different portal colors and put it in the
        /// middle with the spinning part". Two gallery looks in one spot: the base at full size, the inner one scaled down
        /// and lifted so its middle sits on the base's middle (the models' sorting spheres), both facing the admin.
        /// </summary>
        [CommandHandler("acxportalmix", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2,
            "Puts one portal look (by /acxportals number) inside another, 5 m in front of you; replaces your last mix.",
            "<base #> <inner #> [inner size, default 0.5] [inner color palette or -] [raise or lower the inner, meters] [picture: red, blue, ... or a 05 texture id]")]
        public static void HandleAcxPortalMix(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (!int.TryParse(parameters[0], out var b) || !int.TryParse(parameters[1], out var n) || b < 1 || n < 1 || b > Looks.Length || n > Looks.Length)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Use gallery numbers 1-{Looks.Length}: /acxportalmix 17 56 0.5");
                return;
            }
            var scale = parameters.Length > 2 && float.TryParse(parameters[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var sc) ? sc : 0.5f;
            int? palette = parameters.Length > 3 && int.TryParse(parameters[3], out var pal) ? pal : null;
            var lift = parameters.Length > 4 && float.TryParse(parameters[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? l : 0f;
            uint? picture = null;
            if (parameters.Length > 5)
            {
                if (Pictures.TryGetValue(parameters[5], out var named)) picture = named;
                else if (uint.TryParse(parameters[5].Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) picture = hex;
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Pictures: " + string.Join(", ", Pictures.Keys) + ", or a texture id like 0500176C.");
                    return;
                }
            }

            if (mixes.TryRemove(player.Guid.Full, out var old))
                foreach (var o in old) o.Destroy();

            var spot = player.Location.InFrontOf(5, true);
            spot.LandblockId = new LandblockId(spot.GetCell());
            if (player.Location.Indoors == false)
                spot.AdjustMapCoords();

            var outer = WorldObjectFactory.CreateNewWorldObject(Looks[b - 1]);
            var inner = WorldObjectFactory.CreateNewWorldObject(Looks[n - 1]);
            if (outer == null || inner == null) return;

            float Middle(WorldObject wo) => DatManager.PortalDat.ReadFromDat<SetupModel>(wo.SetupTableId).SortingSphere?.Origin.Z ?? 1.4f;
            var innerSpot = new Position(spot);
            innerSpot.PositionZ = spot.PositionZ + Middle(outer) * (outer.ObjScale ?? 1) - Middle(inner) * scale + lift;

            outer.Location = spot;
            outer.Name = $"Mix base #{b}: {outer.Name}";
            outer.Lifespan = 3600;
            inner.Location = innerSpot;
            inner.Name = $"Mix inner #{n}";
            inner.ObjScale = scale;
            if (palette != null) inner.PaletteTemplate = palette;
            inner.Ethereal = true;
            if (picture != null)
            {
                // repaint every texture on the inner model (a picture card has one) with the chosen picture
                var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(inner.SetupTableId);
                for (var part = 0; part < setup.Parts.Count; part++)
                    foreach (var surfaceId in DatManager.PortalDat.ReadFromDat<GfxObj>(setup.Parts[part]).Surfaces)
                    {
                        var surface = DatManager.PortalDat.ReadFromDat<Surface>(surfaceId);
                        if (surface.OrigTextureId != 0)
                            inner.SetAcxTextureSwap((byte)part, surface.OrigTextureId, picture.Value);
                    }
            }
            inner.Lifespan = 3600;
            outer.EnterWorld();
            inner.EnterWorld();
            mixes[player.Guid.Full] = new List<WorldObject> { outer, inner };
            CommandHandlerHelper.WriteOutputInfo(session, $"Mix: #{b} with #{n} inside at size {scale.ToString(CultureInfo.InvariantCulture)}{(palette != null ? $", color {palette}" : "")}{(lift != 0 ? $", raised {lift.ToString(CultureInfo.InvariantCulture)} m" : "")}{(picture != null ? $", picture {parameters[5]}" : "")}.");
        }
    }
}
