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
    /// /mr, /who, /fso, and the unique-player counts in /pop (PlayerCommands.HandlePop). /autofillcomps and /vault dump
    /// were removed 2026-11-25 (JJ: "remove vault dump and autofillcomps from the server too").
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
