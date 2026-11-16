using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using ACE.Database.Models.World;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: special-property pairs (JJ 2026-11-12). A weapon can never hold both halves of a pair: an elemental
    /// cleave refuses that element's rending gem, Biting Strike refuses Critical Strike (Black Opal), Crushing Blow
    /// refuses Crippling Blow (Fire Opal), Armor Cleaving refuses Armor Rending (Sunstone). Any other imbue is still
    /// allowed (one per item, as in retail).
    /// </summary>
    public partial class RecipeManager
    {
        /// <summary>
        /// Called from UseObjectOnTarget after the requirements pass: tells the player and returns true if this recipe's
        /// imbue would complete a pair on the target
        /// </summary>
        public static bool AcxImbuePairBlocks(Player player, Recipe recipe, WorldObject target)
        {
            foreach (var dataId in recipe.RecipeMod.Select(m => (uint)m.DataId).Where(d => d != 0))
            {
                if (target.IsAcxAtlanWeapon && AcxRendingImbues.Contains(dataId))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Atlan weapons can't take rending, so the {target.NameWithMaterial} can't take this imbue.", ChatMessageType.Craft));
                    return true;
                }

                var reason = AcxPairReason(dataId, target);
                if (reason == null)
                    continue;

                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} already has {reason}, the other half of that pair, so it can't take this imbue.", ChatMessageType.Craft));
                return true;
            }
            return false;
        }

        // the rending imbues: Red Garnet, Aquamarine, Emerald, Jet, White Sapphire, Black Garnet, Imperial Topaz, and Tiger Eye on a caster
        private static readonly System.Collections.Generic.HashSet<uint> AcxRendingImbues = new System.Collections.Generic.HashSet<uint>()
        {
            0x3800003E, 0x3800003C, 0x3800003A, 0x3800003D, 0x3800003B, 0x3800003F, 0x38000040, AcxDataIdBase + (uint)AcxTinker.ElementalRend,
        };

        private static string AcxPairReason(uint dataId, WorldObject target)
        {
            var cleave = target.ResistanceModifierType;
            string Cleave(DamageType type) => cleave == type ? $"{type.ToString().ToLower()} cleaving" : null;

            return dataId switch
            {
                0x3800003E => Cleave(DamageType.Fire),       // Red Garnet: fire rending
                0x3800003C => Cleave(DamageType.Cold),       // Aquamarine: cold rending
                0x3800003A => Cleave(DamageType.Acid),       // Emerald: acid rending
                0x3800003D => Cleave(DamageType.Electric),   // Jet: lightning rending
                0x3800003B => Cleave(DamageType.Bludgeon),   // White Sapphire: bludgeon rending
                0x3800003F => Cleave(DamageType.Pierce),     // Black Garnet: pierce rending
                0x38000040 => Cleave(DamageType.Slash),      // Imperial Topaz: slash rending
                AcxDataIdBase + (uint)AcxTinker.ElementalRend =>   // Tiger Eye: all four elemental rends on a caster
                    cleave == DamageType.Fire || cleave == DamageType.Cold || cleave == DamageType.Acid || cleave == DamageType.Electric ? "elemental cleaving" : null,
                0x38000023 => target.CriticalFrequency != null ? "Biting Strike" : null,                                  // Black Opal: Critical Strike
                0x38000024 => target.GetProperty(PropertyFloat.CriticalMultiplier) != null ? "Crushing Blow" : null,      // Fire Opal: Crippling Blow
                0x38000025 => target.IgnoreArmor != null ? "Armor Cleaving" : null,                                       // Sunstone: Armor Rending
                _ => null,
            };
        }
    }
}
