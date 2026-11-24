using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: reload a landblock by its id, from the server console (JJ 2026-11-24, on new Concordia garbage barrels:
    /// "I can't reload the landblock not on admin"). The same steps as /reload-landblock, which only reloads the landblock
    /// its admin stands in: every non-player object destroyed, the landblock's cached instances cleared, then Init again.
    /// </summary>
    public static class ACXLandblockCommands
    {
        [CommandHandler("acxreloadlandblock", AccessLevel.Admin, CommandHandlerFlag.None, 1,
            "Reloads a loaded landblock by id (e.g. 018A for Concordia), so new content shows without a restart.",
            "<landblock hex>")]
        public static void HandleReload(Session session, params string[] parameters)
        {
            if (!uint.TryParse(parameters[0].Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var id))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: acxreloadlandblock <landblock hex, e.g. 018A>");
                return;
            }

            var landblockId = new LandblockId(((id > 0xFFFF ? id >> 16 : id) << 16) | 0xFFFF);
            if (!LandblockManager.IsLoaded(landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Landblock {landblockId.Landblock:X4} isn't loaded; it picks up the changes when it next loads.");
                return;
            }

            var landblock = LandblockManager.GetLandblock(landblockId, false);
            var actionChain = new ActionChain();
            actionChain.AddAction(landblock, () =>
            {
                landblock.DestroyAllNonPlayerObjects();
                DatabaseManager.World.ClearCachedInstancesByLandblock(landblock.Id.Landblock);
            });
            actionChain.AddDelayForOneTick();
            actionChain.AddAction(landblock, () => landblock.Init(true));
            actionChain.EnqueueChain();

            CommandHandlerHelper.WriteOutputInfo(session, $"Reloading landblock {landblockId.Landblock:X4}.");
        }
    }
}
