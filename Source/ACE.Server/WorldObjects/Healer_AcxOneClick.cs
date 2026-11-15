using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: one-click healing (JJ 2026-11-13, from ClassicACE). Kits (health, stamina and mana) lose their "use on a
    /// target" flags when they load, so double-clicking one is a plain use with no targeting cursor, and
    /// Player.AcxOneClickHeal heals the selected player or yourself. Turning acx_one_click_heal off gives kits their
    /// weenie's flags back.
    /// </summary>
    partial class Healer
    {
        private void AcxOneClickUseable()
        {
            if (PropertyManager.GetBool("acx_one_click_heal").Item)
            {
                if (ItemUseable is Usable usable && usable.GetTargetFlags() != Usable.Undef)
                    ItemUseable = usable & ~usable.GetTargetFlags();
            }
            else
            {
                var original = (Usable?)DatabaseManager.World.GetCachedWeenie(WeenieClassId)?.GetProperty(PropertyInt.ItemUseable);
                if (original != null && ItemUseable != original)
                    ItemUseable = original;
            }
        }
    }
}
