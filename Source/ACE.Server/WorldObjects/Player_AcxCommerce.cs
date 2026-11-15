using ACE.Entity.Enum;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: vendor rules
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// ACX scrolls and vault items resell for 1 pyreal: the copy the player receives is worth 1, while the shop price
        /// (weenie value x vendor markup) is unchanged. Vendors never pay less than 1, so any markdown still pays 1.
        /// </summary>
        private static void SetAcxResaleValue(WorldObject item)
        {
            if (item.WeenieType != WeenieType.SpellTransferScroll && !AcxRevive.IsScroll(item) && item.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.AcxCraftGuarantee) == null && !AcxVaults.IsTool(item))
                return;

            if ((item.MaxStackSize ?? 1) > 1)
            {
                item.StackUnitValue = 1;
                item.Value = item.StackSize ?? 1;
            }
            else
                item.Value = 1;
        }
    }
}
