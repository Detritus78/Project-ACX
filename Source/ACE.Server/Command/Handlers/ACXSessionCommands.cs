using System;
using System.Linq;
using System.Text;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: who is online from where (JJ 2026-11-25, with the 3-per-address login cap, MaximumAllowedSessionsPerIPAddress:
    /// "yes add the command"). Lists online characters grouped by address, with account, level, fellowship, landblock and time
    /// online, so one person running more than 3 through a VPN shows up as characters that play together from different
    /// addresses. Admin only; runs from the console.
    /// </summary>
    public static class ACXSessionCommands
    {
        [CommandHandler("acxsessions", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Lists online characters by internet address: account, level, fellowship, landblock and minutes online.")]
        public static void HandleSessions(Session session, params string[] parameters)
        {
            var now = Time.GetUnixTime();
            var online = PlayerManager.GetAllOnline().Where(p => p.Session != null).ToList();
            var text = new StringBuilder($"Online: {online.Count}\n");

            foreach (var group in online.GroupBy(p => p.Session.EndPointC2S?.Address?.ToString() ?? "?").OrderByDescending(g => g.Count()))
            {
                text.Append($"{group.Key}  ({group.Count()})\n");
                foreach (var p in group.OrderBy(p => p.Session.Account))
                {
                    var login = p.GetProperty(PropertyFloat.LoginTimestamp) ?? now;
                    var minutes = (int)Math.Max(0, (now - login) / 60);
                    var fellow = p.Fellowship?.FellowshipName ?? "-";
                    var landblock = p.Location != null ? $"{p.Location.Landblock:X4}" : "?";
                    text.Append($"  {p.Session.Account} / {p.Name}, level {p.Level}, fellowship {fellow}, landblock {landblock}, {minutes} min\n");
                }
            }

            CommandHandlerHelper.WriteOutputInfo(session, text.ToString());
        }
    }
}
