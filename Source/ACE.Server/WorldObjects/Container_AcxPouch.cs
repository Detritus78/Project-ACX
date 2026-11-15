using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: component and gem pouches (JJ 2026-11-11, ported from ClassicACE). A pouch is a side pack that only
    /// holds one kind of item (AcxPouchItemTypes, int 30011): the Spell Component Pouch spell components, the Gem Pouch
    /// gems. Items never fall into a pouch they don't belong in, and a pouch and its contents are safe on death.
    /// Pouches: acx/Content/crafting/20-pouches.sql.
    /// </summary>
    partial class WorldObject
    {
        public bool AcxIsPouch => (GetProperty(PropertyInt.AcxPouchItemTypes) ?? 0) != 0;
    }

    partial class Container
    {
        public bool AcxAccepts(WorldObject item)
        {
            var types = (ItemType)(GetProperty(PropertyInt.AcxPouchItemTypes) ?? 0);
            return types == 0 || (item.ItemType & types) != 0;
        }

        public string AcxPouchContents
        {
            get
            {
                var types = (ItemType)(GetProperty(PropertyInt.AcxPouchItemTypes) ?? 0);
                return types == ItemType.SpellComponents ? "spell components" : types == ItemType.Gem ? "gems" : types.ToString().ToLower();
            }
        }
    }
}
