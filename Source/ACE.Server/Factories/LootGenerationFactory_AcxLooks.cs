using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Project ACX: custom looks for generated loot
    /// </summary>
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Deep purple texture JJ picked for nether weapons (average colour #2F004E)
        /// </summary>
        public const uint NetherWeaponTexture = 0x0500303D;

        /// <summary>
        /// Penumbral Horror smoke, stacked three times
        /// </summary>
        public const uint NetherWeaponAuraScript = 0x33000F82;
        public const int NetherWeaponAuraScriptCount = 3;

        private static readonly ConcurrentDictionary<uint, (byte Part, uint Texture)> mainTextureCache = new ConcurrentDictionary<uint, (byte Part, uint Texture)>();

        private static void ApplyAcxLootLooks(WorldObject wo)
        {
            if (wo == null || wo.W_DamageType != DamageType.Nether || (wo.ItemType & ItemType.WeaponOrCaster) == 0)
                return;

            ApplyNetherWeaponLook(wo);
        }

        public static void ApplyNetherWeaponLook(WorldObject wo)
        {
            var main = GetMainTexture(wo.SetupTableId);
            if (main.Texture != 0)
                wo.SetAcxTextureSwap(main.Part, main.Texture, NetherWeaponTexture);

            wo.AcxAuraScript = NetherWeaponAuraScript;
            wo.AcxAuraScriptCount = NetherWeaponAuraScriptCount;
        }

        /// <summary>
        /// The texture covering the most surface area of a model, e.g. a sword's blade (05000C53 on the Long Sword)
        /// </summary>
        public static (byte Part, uint Texture) GetMainTexture(uint setupId)
        {
            return mainTextureCache.GetOrAdd(setupId, id =>
            {
                (byte Part, uint Texture) best = (0, 0);
                var bestArea = 0.0;

                try
                {
                    var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(id);

                    for (var part = 0; part < setup.Parts.Count && part <= byte.MaxValue; part++)
                    {
                        var gfx = DatManager.PortalDat.ReadFromDat<GfxObj>(setup.Parts[part]);
                        var areaByTexture = new Dictionary<uint, double>();

                        foreach (var polygon in gfx.Polygons.Values)
                        {
                            if (polygon.PosSurface < 0 || polygon.PosSurface >= gfx.Surfaces.Count || polygon.VertexIds.Count < 3)
                                continue;

                            var texture = DatManager.PortalDat.ReadFromDat<Surface>(gfx.Surfaces[polygon.PosSurface]).OrigTextureId;
                            if (texture == 0)
                                continue;

                            var origin = gfx.VertexArray.Vertices[(ushort)polygon.VertexIds[0]].Origin;
                            var area = 0.0;
                            for (var i = 1; i + 1 < polygon.VertexIds.Count; i++)
                            {
                                var b = gfx.VertexArray.Vertices[(ushort)polygon.VertexIds[i]].Origin;
                                var c = gfx.VertexArray.Vertices[(ushort)polygon.VertexIds[i + 1]].Origin;
                                area += Vector3.Cross(b - origin, c - origin).Length() / 2;
                            }
                            areaByTexture[texture] = areaByTexture.GetValueOrDefault(texture) + area;
                        }

                        foreach (var entry in areaByTexture)
                        {
                            if (entry.Value <= bestArea)
                                continue;

                            bestArea = entry.Value;
                            best = ((byte)part, entry.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"GetMainTexture({id:X8}) - {ex.Message}");
                }

                return best;
            });
        }
    }
}
