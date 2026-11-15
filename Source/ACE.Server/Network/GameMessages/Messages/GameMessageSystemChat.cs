using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageSystemChat : GameMessage
    {
        public ChatMessageType ChatType { get; }   // Project ACX: lets BroadcastToAll spot world broadcasts

        public GameMessageSystemChat(string message, ChatMessageType chatMessageType)
            : base(GameMessageOpcode.ServerMessage, GameMessageGroup.UIQueue)
        {
            ChatType = chatMessageType;
            Writer.WriteString16L(message);
            Writer.Write((int)chatMessageType);
        }
    }
}
