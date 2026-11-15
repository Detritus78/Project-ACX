using System;
using System.Linq;
using System.Text;

using ACE.Common;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: quality-of-life commands ported from ClassicACE and Pourkarutide (JJ 2026-11-11):
    /// /mr, /who, /autofillcomps, /fso, and the unique-player counts in /pop (PlayerCommands.HandlePop).
    /// </summary>
    public static class ACXQolCommands
    {
        [CommandHandler("mr", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Recalls you to your allegiance's mansion (the same as /house mansion_recall).")]
        public static void HandleMansionRecall(Session session, params string[] parameters)
        {
            session.Player.HandleActionTeleToMansion();
        }

        [CommandHandler("who", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Lists the members of your allegiance who are online.")]
        public static void HandleWho(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player.MonarchId == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You're not in an allegiance.");
                return;
            }

            var members = PlayerManager.GetAllOnline().Where(p => p.MonarchId == player.MonarchId).OrderBy(p => p.Name).ToList();
            var text = new StringBuilder($"Allegiance members online ({members.Count}):\n");
            foreach (var m in members)
                text.Append($"  {m.Name}, level {m.Level}\n");
            CommandHandlerHelper.WriteOutputInfo(session, text.ToString());
        }

        [CommandHandler("autofillcomps", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
            "Sets your component fill levels (the spell components panel) from the spells you know.",
            "<multiplier 1-500> | clear\nEach component gets 10 x the multiplier (scarabs and talismans 3 x), at most 5,000. Relog to see the new values.")]
        public static void HandleAutoFillComps(Session session, params string[] parameters)
        {
            var player = session.Player;
            var existing = player.Character.GetFillComponents(player.CharacterDatabaseLock).Select(c => (uint)c.SpellComponentId).ToList();

            if (parameters[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var wcid in existing)
                    player.Character.TryRemoveFillComponent(wcid, out _, player.CharacterDatabaseLock);
                player.CharacterChangesDetected = true;
                CommandHandlerHelper.WriteOutputInfo(session, "Component fill levels cleared. Relog to see it.");
                return;
            }

            if (!int.TryParse(parameters[0], out var multiplier) || multiplier < 1)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: /autofillcomps <multiplier> (for example 10), or /autofillcomps clear");
                return;
            }
            multiplier = Math.Clamp(multiplier, 1, 500);

            var count = 0;
            foreach (var spellId in player.Biota.GetKnownSpellsIds(player.BiotaDatabaseLock))
            {
                var spell = new Spell(spellId);
                if (spell.Formula?.Components == null)
                    continue;

                foreach (var componentId in spell.Formula.Components)
                {
                    if (!SpellFormula.SpellComponentsTable.SpellComponents.TryGetValue(componentId, out var component))
                        continue;

                    var wcid = Spell.GetComponentWCID(componentId);
                    if (!SpellComponent.IsValid(wcid))
                        continue;

                    var each = component.Type == (int)ACE.DatLoader.FileTypes.SpellComponentsTable.Type.Scarab || component.Type == (int)ACE.DatLoader.FileTypes.SpellComponentsTable.Type.Talisman ? 3 : 10;
                    player.Character.TryRemoveFillComponent(wcid, out _, player.CharacterDatabaseLock);
                    player.Character.AddFillComponent(wcid, (uint)Math.Min(each * multiplier, 5000), player.CharacterDatabaseLock, out _);
                    count++;
                }
            }
            player.CharacterChangesDetected = true;
            CommandHandlerHelper.WriteOutputInfo(session, $"Component fill levels set from your spellbook ({count} entries). Relog to see the new values.");
        }

        [CommandHandler("fso", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Fixes an off-hand item icon that's stuck as wielded.")]
        public static void HandleFixStuckOffhand(Session session, params string[] parameters)
        {
            session.Player.AcxFixStuckEquippedIcon(EquipMask.Shield);
        }

        /// <summary>
        /// Called from /pop: unique players (accounts) seen in the last day and week
        /// </summary>
        public static string UniquePlayersText()
        {
            var now = Time.GetUnixTime();
            int Unique(double seconds) => PlayerManager.GetAllPlayers()
                .Where(p => p.Account != null && (PlayerManager.GetOnlinePlayer(p.Guid) != null || now - (p.GetProperty(PropertyFloat.LoginTimestamp) ?? 0) <= seconds))
                .Select(p => p.Account.AccountId).Distinct().Count();
            return $"Unique players in the last 24 hours: {Unique(86400):N0}. In the last 7 days: {Unique(7 * 86400):N0}.";
        }
    }
}
