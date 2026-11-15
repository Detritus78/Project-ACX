using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        public static WorldObject CreateMissileWeapon(TreasureDeath profile, bool isMagical, bool mutate = true)
        {
            // this function is only used by test methods, and is not part of regular lootgen
            var treasureRoll = new TreasureRoll(TreasureItemType.Weapon);
            treasureRoll.WeaponType = WeaponTypeChance.MissileChances.Roll();
            treasureRoll.Wcid = WeaponWcids.Roll(profile, ref treasureRoll.WeaponType);

            var wo = WorldObjectFactory.CreateNewWorldObject((uint)treasureRoll.Wcid);

            MutateMissileWeapon(wo, profile, isMagical, treasureRoll);
            
            return wo;
        }

        private static void MutateMissileWeapon(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll)
        {
            // new method / mutation scripts
            var isElemental = wo.W_DamageType != DamageType.Undef;

            // Project ACX: an Eternal thrown weapon (AcxThrownWcids) carries its own damage type, which would always pick
            // the elemental atlatl script; it rolls as a plain atlatl through tier 4 and as an elemental one from tier 5,
            // like atlatls do, for the wield requirement and the spread from trash to good
            var isEternalThrown = AcxThrownWcids.IsEternalThrown(wo.WeenieClassId);

            var scriptName = isEternalThrown ? GetMissileScript(TreasureWeaponType.Atlatl, profile.Tier >= 5) : GetMissileScript(roll.WeaponType, isElemental);

            // mutate DamageMod / ElementalDamageBonus / WieldRequirements
            var mutationFilter = MutationCache.GetMutation(scriptName);

            mutationFilter.TryMutate(wo, profile.Tier);

            // Project ACX: the rolled elemental bonus becomes part of the multiplier (thrown weapons have no launcher to
            // add it), then the multiplier is scaled to the thrown band, and it all goes into the weapon's own damage with
            // a damage range of its own, like a melee weapon's (AcxThrownWcids; the projectile carries both: CarryRoll)
            if (isEternalThrown && wo.DamageMod != null)
            {
                if (wo.ElementalDamageBonus != null)
                {
                    wo.DamageMod *= AcxThrownWcids.QualityFromBonus(profile.Tier, wo.ElementalDamageBonus.Value);
                    wo.ElementalDamageBonus = null;
                }
                wo.DamageMod *= AcxThrownWcids.GetDamageModScale(profile.Tier) * AcxThrownWcids.RollQuality(profile.Tier);

                wo.Damage = (int)System.Math.Round((wo.Damage ?? 0) * wo.DamageMod.Value);
                wo.DamageMod = null;
                wo.DamageVariance = AcxThrownWcids.RollVariance();
            }

            // mutate WeaponDefense
            mutationFilter = MutationCache.GetMutation("MissileWeapons.weapon_defense.txt");

            mutationFilter.TryMutate(wo, profile.Tier);

            // weapon speed
            if (wo.WeaponTime != null)
            {
                var weaponSpeedMod = RollWeaponSpeedMod(profile);
                wo.WeaponTime = (int)(wo.WeaponTime * weaponSpeedMod);
            }

            // material type
            var materialType = GetMaterialType(wo, profile.Tier);
            if (materialType > 0)
                wo.MaterialType = materialType;

            // item color
            MutateColor(wo);

            // gem count / gem material
            if (wo.GemCode != null)
                wo.GemCount = GemCountChance.Roll(wo.GemCode.Value, profile.Tier);
            else
                wo.GemCount = ThreadSafeRandom.Next(1, 5);

            wo.GemType = RollGemType(profile.Tier);

            // workmanship
            wo.ItemWorkmanship = WorkmanshipChance.Roll(profile.Tier);

            // burden
            MutateBurden(wo, profile, true);

            // missile / magic defense
            wo.WeaponMissileDefense = MissileMagicDefense.Roll(profile.Tier);
            wo.WeaponMagicDefense = MissileMagicDefense.Roll(profile.Tier);

            // spells
            if (!isMagical)
            {
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
                wo.ManaRate = null;
            }
            else
                AssignMagic(wo, profile, roll);

            // item value
            //if (wo.HasMutateFilter(MutateFilter.Value))   // fixme: data
                MutateValue(wo, profile.Tier, roll);

            // long description
            wo.LongDesc = GetLongDesc(wo);

            RollAcxExtraMutations(wo, profile); // Project ACX: extra weapon mutations

            ApplyAcxLootLooks(wo); // Project ACX
        }

        private static string GetMissileScript(TreasureWeaponType weaponType, bool isElemental = false)
        {
            var elementalStr = isElemental ? "elemental" : "non_elemental";

            return "MissileWeapons." + weaponType.GetScriptName() + "_" + elementalStr + ".txt";
        }
    }
}
