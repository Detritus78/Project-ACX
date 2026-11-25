using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Entity.Models;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: vendor reputation (JJ 2026-11-12). What vendors charge never changes. What they pay for items starts at
    /// 50% of value everywhere and rises with a character's reputation in that town, built from the pyreals they've traded
    /// (bought plus sold) with the town's vendors: 60% at 50,000, 70% at 250,000, 80% at 1 million, 90% at 5 million
    /// (since 2026-11-25, JJ: "go with your steps"; was 100k/1M/10M/100M, out of reach at the economy log's rates).
    /// It always stays below what vendors charge, so buying and selling back loses money. Trade notes stay outside it.
    /// Vendors in Concordia (the Pawn Shopkeep in the gambling hall) pay a flat acx_concordia_buy_rate, under every town's
    /// starting 50%, and trade there builds no reputation: a buyer for big spenders whose bags fill up, not the place to sell
    /// (JJ 2026-11-17: "We have to have a buyer in a gambling hall for the big spenders. Otherwise their bags fill up and they
    /// have to travel. Maybe we omit him from rep to buy higher and actually reduce his % a bit to discourage making him the
    /// main").
    /// </summary>
    public static class AcxVendorReputation
    {
        private static readonly (long Traded, double Rate)[] Tiers =
        {
            (5_000_000, 0.9), (1_000_000, 0.8), (250_000, 0.7), (50_000, 0.6), (0, 0.5)
        };

        private const int TownRange = 6;   // landblocks

        private static readonly ConcurrentDictionary<uint, string> vendorTowns = new();
        private static List<(string Name, ushort Landblock)> towns;

        private static readonly HashSet<string> NotTowns = new(StringComparer.OrdinalIgnoreCase)
        {
            "Hotel", "Hotel Swank", "HotelSwank", "Swank", "Night Club", "NightClub", "Storage", "TN", "Town Network", "TownNetwork", "Underground"
        };

        private static List<(string Name, ushort Landblock)> Towns
        {
            get
            {
                if (towns != null)
                    return towns;

                var list = new List<(string Name, ushort Landblock)>();
                using (var ctx = new ACE.Database.Models.World.WorldDbContext())
                {
                    foreach (var poi in ctx.PointsOfInterest.ToList())
                    {
                        if (NotTowns.Contains(poi.Name))
                            continue;
                        var dest = DatabaseManager.World.GetCachedWeenie(poi.WeenieClassId)?.GetPosition(PositionType.Destination);
                        if (dest != null)
                            list.Add((poi.Name == "Outpost" ? "Danby's Outpost" : poi.Name, (ushort)(dest.Cell >> 16)));
                    }
                }
                list.Add(("Candeth Keep", 0x2B12));
                list.Add(("Wai Jhou", 0x3F31));
                towns = list;
                return towns;
            }
        }

        /// <summary>
        /// The town a vendor belongs to: the town on its own landblock, else the nearest within 6 landblocks outdoors,
        /// else its landblock on its own
        /// </summary>
        private static string GetTown(Vendor vendor)
        {
            return vendorTowns.GetOrAdd(vendor.Guid.Full, _ =>
            {
                var lb = (ushort)(vendor.Location.LandblockId.Raw >> 16);
                var exact = Towns.FirstOrDefault(t => t.Landblock == lb);
                if (exact.Name != null)
                    return exact.Name;

                if (!vendor.Location.Indoors || !(vendor.CurrentLandblock?.IsDungeon ?? false))
                {
                    int x = lb >> 8, y = lb & 0xFF;
                    var nearest = Towns.Select(t => (t.Name, d: Math.Max(Math.Abs((t.Landblock >> 8) - x), Math.Abs((t.Landblock & 0xFF) - y))))
                        .Where(t => t.d <= TownRange).OrderBy(t => t.d).FirstOrDefault();
                    if (nearest.Name != null)
                        return nearest.Name;
                }
                return $"lb {lb:X4}";
            });
        }

        private static Dictionary<string, long> GetTrade(Player player)
        {
            var trade = new Dictionary<string, long>();
            foreach (var entry in (player.GetProperty(PropertyString.AcxVendorTrade) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = entry.LastIndexOf('=');
                if (eq > 0 && long.TryParse(entry[(eq + 1)..], out var amount))
                    trade[entry[..eq]] = amount;
            }
            return trade;
        }

        private static double RateFor(long traded) => Tiers.First(t => traded >= t.Traded).Rate;

        // Concordia is the landblock the vaults stand in (acx_vault_landblock)
        private static bool InConcordia(Vendor vendor) => vendor.Location != null && (vendor.Location.LandblockId.Raw >> 16) == (uint)PropertyManager.GetLong("acx_vault_landblock").Item;

        /// <summary>
        /// What this vendor pays this player, as a share of an item's value
        /// </summary>
        public static double GetBuyRate(Player player, Vendor vendor)
        {
            if (InConcordia(vendor))
                return PropertyManager.GetDouble("acx_concordia_buy_rate").Item;
            if (player == null)
                return RateFor(0);
            return GetTrade(player).TryGetValue(GetTown(vendor), out var traded) ? RateFor(traded) : RateFor(0);
        }

        /// <summary>
        /// Adds pyreals traded with a vendor to the player's reputation in its town, and tells them when the rate goes up
        /// </summary>
        public static void AddTrade(Player player, Vendor vendor, long pyreals)
        {
            if (pyreals <= 0 || InConcordia(vendor))
                return;

            var town = GetTown(vendor);
            var trade = GetTrade(player);
            var before = trade.TryGetValue(town, out var t) ? t : 0;
            trade[town] = before + pyreals;
            player.SetProperty(PropertyString.AcxVendorTrade, string.Join(";", trade.Select(kv => $"{kv.Key}={kv.Value}")));

            var oldRate = RateFor(before);
            var newRate = RateFor(before + pyreals);
            if (newRate > oldRate)
            {
                var where = town.StartsWith("lb ") ? "These merchants" : $"{town}'s merchants";
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{where} know you well now: they pay you {newRate:P0} of an item's value.", ChatMessageType.Broadcast));
            }
        }
    }
}
