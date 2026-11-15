using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: crafting guarantee scrolls (JJ 2026-11-11). Reading one makes one kind of craft succeed 100% for
    /// 10 minutes: Tinkering (salvage), Imbue, Alchemy, Cooking or Fletching. Retail failure risk stays otherwise. No buff
    /// icon (the client can't show new spells): the timer lives on the server (AcxCraftGuarantees, string 30009, kept
    /// through logout), with chat warnings and /guarantees for the time left. Reading another of the same kind adds 10
    /// minutes. 250,000 pyreals each at the mage vendors that sell spell extraction scrolls; they resell for 1.
    /// Scrolls: acx/Content/crafting/30-craft-guarantee-scrolls.sql (AcxCraftGuarantee int 30012 = the kind).
    /// </summary>
    partial class Player
    {
        public enum AcxCraft { Tinkering = 1, Imbue, Alchemy, Cooking, Fletching }

        private const double AcxGuaranteeSeconds = 600;
        private const double AcxGuaranteeWarnSeconds = 60;

        private readonly HashSet<AcxCraft> acxGuaranteeWarned = new();

        private Dictionary<AcxCraft, double> AcxGuarantees()
        {
            var result = new Dictionary<AcxCraft, double>();
            foreach (var entry in (GetProperty(PropertyString.AcxCraftGuarantees) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[0], out var kind) && double.TryParse(parts[1], out var until))
                    result[(AcxCraft)kind] = until;
            }
            return result;
        }

        private void AcxSaveGuarantees(Dictionary<AcxCraft, double> guarantees)
        {
            if (guarantees.Count == 0)
                RemoveProperty(PropertyString.AcxCraftGuarantees);
            else
                SetProperty(PropertyString.AcxCraftGuarantees, string.Join(";", guarantees.Select(g => $"{(int)g.Key}={g.Value:0}")));
        }

        /// <summary>
        /// Called from GenericObject.ActOnUse: reading a crafting guarantee scroll uses it up
        /// </summary>
        public void AcxReadCraftScroll(WorldObject scroll)
        {
            var kind = (AcxCraft)(scroll.GetProperty(PropertyInt.AcxCraftGuarantee) ?? 0);
            if (!Enum.IsDefined(kind) || !TryConsumeFromInventoryWithNetworking(scroll, 1))
                return;

            var now = Time.GetUnixTime();
            var guarantees = AcxGuarantees();
            var start = guarantees.TryGetValue(kind, out var until) && until > now ? until : now;
            guarantees[kind] = start + AcxGuaranteeSeconds;
            AcxSaveGuarantees(guarantees);
            acxGuaranteeWarned.Remove(kind);

            Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {kind.ToString().ToLower()} can't fail for the next {AcxTimeText(guarantees[kind] - now)}. /guarantees shows the time left.", ChatMessageType.Craft));
        }

        /// <summary>
        /// Called from RecipeManager.GetRecipeChance: true while a guarantee covers this recipe's kind of craft
        /// </summary>
        public bool AcxCraftGuaranteed(Recipe recipe)
        {
            AcxCraft? kind = recipe.IsImbuing() ? AcxCraft.Imbue
                : recipe.IsTinkering() ? AcxCraft.Tinkering
                : (Skill)recipe.Skill switch { Skill.Alchemy => AcxCraft.Alchemy, Skill.Cooking => AcxCraft.Cooking, Skill.Fletching => AcxCraft.Fletching, _ => null };

            return kind != null && AcxGuarantees().TryGetValue(kind.Value, out var until) && until > Time.GetUnixTime();
        }

        /// <summary>
        /// Called from Heartbeat: a warning a minute before a guarantee ends, and a message when it does
        /// </summary>
        public void AcxCraftGuaranteeTick()
        {
            var guarantees = AcxGuarantees();
            if (guarantees.Count == 0)
                return;

            var now = Time.GetUnixTime();
            var changed = false;
            foreach (var (kind, until) in guarantees.ToList())
            {
                if (until <= now)
                {
                    guarantees.Remove(kind);
                    acxGuaranteeWarned.Remove(kind);
                    changed = true;
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {kind.ToString().ToLower()} guarantee has run out.", ChatMessageType.Craft));
                }
                else if (until - now <= AcxGuaranteeWarnSeconds && acxGuaranteeWarned.Add(kind))
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {kind.ToString().ToLower()} guarantee runs out in {AcxTimeText(until - now)}.", ChatMessageType.Craft));
            }
            if (changed)
                AcxSaveGuarantees(guarantees);
        }

        public string AcxGuaranteesText()
        {
            var now = Time.GetUnixTime();
            var active = AcxGuarantees().Where(g => g.Value > now).ToList();
            return active.Count == 0 ? "You have no crafting guarantees."
                : "Crafting guarantees: " + string.Join(", ", active.Select(g => $"{g.Key} {AcxTimeText(g.Value - now)}")) + ".";
        }

        private static string AcxTimeText(double seconds)
        {
            var s = (int)Math.Ceiling(seconds);
            return s >= 60 ? $"{s / 60}:{s % 60:00} minutes" : $"{s} seconds";
        }
    }
}
