namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: a corpse whose owner is waiting beside it as a ghost (Player_AcxGhost.cs)
    /// </summary>
    partial class Corpse
    {
        /// <summary>
        /// While set, the corpse isn't cut to the 15 second empty-corpse decay (WorldObject.Decay)
        /// </summary>
        public bool AcxHoldsGhost;
    }
}
