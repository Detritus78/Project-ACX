using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: one-click healing (JJ 2026-11-13, from ClassicACE): double-clicking a kit in your pack heals the player
    /// you have selected, or yourself when nobody (or a monster) is selected. AC:Unreal tells the server about every
    /// selection change (its health query), so the selection is current. The selected player has to be close by, and two
    /// player killers who aren't fellows heal themselves instead of each other, as in ClassicACE.
    /// </summary>
    partial class Player
    {
        private const float AcxOneClickHealRange = 20.0f;

        /// <summary>
        /// Called from HandleActionUseItem: true when the item was a kit and the heal has started
        /// </summary>
        private bool AcxOneClickHeal(WorldObject item)
        {
            if (item is not Healer || item.CurrentLandblock != null || !PropertyManager.GetBool("acx_one_click_heal").Item)
                return false;

            var target = this;
            if (HealthQueryTarget is uint selectedGuid && selectedGuid != Guid.Full && PlayerManager.GetOnlinePlayer(selectedGuid) is Player selected
                && selected.Location != null && Location.DistanceTo(selected.Location) <= AcxOneClickHealRange
                && (!IsPKType || !selected.IsPKType || (Fellowship != null && Fellowship == selected.Fellowship)))
                target = selected;

            HandleActionUseWithTarget(item.Guid.Full, target.Guid.Full);
            return true;
        }
    }
}
