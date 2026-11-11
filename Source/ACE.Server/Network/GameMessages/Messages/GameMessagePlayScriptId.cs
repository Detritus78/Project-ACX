using ACE.Entity;

namespace ACE.Server.Network.GameMessages.Messages
{
    /// <summary>
    /// Project ACX: plays a PhysicsScript (0x33xxxxxx) on an object by its id,
    /// instead of by PlayScript type through the object's PhysicsScriptTable like GameMessageScript
    /// </summary>
    public class GameMessagePlayScriptId : GameMessage
    {
        public GameMessagePlayScriptId(ObjectGuid guid, uint scriptId)
            : base(GameMessageOpcode.PlayScriptId, GameMessageGroup.SmartboxQueue, 12)
        {
            Writer.WriteGuid(guid);
            Writer.Write(scriptId);
        }
    }
}
