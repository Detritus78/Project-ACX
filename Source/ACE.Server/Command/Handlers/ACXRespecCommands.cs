using System.Collections.Generic;

using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Project ACX: an admin skill reset for a logged-in character (JJ 2026-11-24, turning Einar the thrown monarch into Brokk
    /// the tinker: "to make this easier can you reset the skills on brokk?"). The trainers' fresh start without the trainer:
    /// every trained and specialized skill reset with its credits and experience refunded (Player.AcxResetAllSkills; the
    /// free trade skills stay trained). Attributes are left alone: the Concordia gems do those.
    /// </summary>
    public static class ACXRespecCommands
    {
        [CommandHandler("acxresetskills", AccessLevel.Admin, CommandHandlerFlag.None, 1,
            "Resets every trained and specialized skill on a logged-in character, refunding the credits and experience.",
            "<character name>")]
        public static void HandleResetSkills(Session session, params string[] parameters)
        {
            var name = string.Join(" ", parameters).Trim();
            var player = PlayerManager.GetOnlinePlayer(name);
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{name} isn't logged in.");
                return;
            }

            player.AcxResetAllSkills();
            player.SendMessage("Your skills have been reset. Your skill credits and experience are back to spend.");
            CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}'s skills reset: {player.AvailableSkillCredits} credits, {player.AvailableExperience:N0} experience to spend.");
        }

        private static readonly Dictionary<string, (Skill skill, PropertyInt aug)> AugSkills = new Dictionary<string, (Skill, PropertyInt)>
        {
            ["weapon"] = (Skill.WeaponTinkering, PropertyInt.AugmentationSpecializeWeaponTinkering),
            ["armor"] = (Skill.ArmorTinkering, PropertyInt.AugmentationSpecializeArmorTinkering),
            ["item"] = (Skill.ItemTinkering, PropertyInt.AugmentationSpecializeItemTinkering),
            ["magicitem"] = (Skill.MagicItemTinkering, PropertyInt.AugmentationSpecializeMagicItemTinkering),
            ["salvaging"] = (Skill.Salvaging, PropertyInt.AugmentationSpecializeSalvaging),
        };

        /// <summary>
        /// Project ACX: specialize a tinkering skill or Salvaging for skill credits (JJ 2026-11-24: "Can we change it so we can
        /// use skill credits to specize it. If even temporarily? I still want to use these skill credits up."). Retail only
        /// specializes these with the 1-billion-XP Essences (the dat spec cost is 999, so the client offers no button). This
        /// does what an Essence does (sets its augmentation flag, so a later reset keeps the specialization and refunds no
        /// credits, exactly as for an Essence) and charges the skill's trained cost in credits instead of the XP.
        /// </summary>
        [CommandHandler("acxspecialize", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Specializes a tinkering skill or Salvaging on a logged-in character for skill credits (the skill's trained cost), as its Essence would.",
            "<character name>, <weapon | armor | item | magicitem | salvaging>")]
        public static void HandleSpecialize(Session session, params string[] parameters)
        {
            var parts = string.Join(" ", parameters).Split(',');
            if (parts.Length != 2 || !AugSkills.TryGetValue(parts[1].Trim().ToLower().Replace(" ", ""), out var entry))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: acxspecialize <character name>, <weapon | armor | item | magicitem | salvaging>");
                return;
            }

            var player = PlayerManager.GetOnlinePlayer(parts[0].Trim());
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{parts[0].Trim()} isn't logged in.");
                return;
            }

            var creatureSkill = player.GetCreatureSkill(entry.skill);
            if (creatureSkill.AdvancementClass != SkillAdvancementClass.Trained)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}'s {entry.skill} is {creatureSkill.AdvancementClass}, not trained.");
                return;
            }

            var cost = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)entry.skill].TrainedCost;
            if ((player.AvailableSkillCredits ?? 0) < cost)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name} has {player.AvailableSkillCredits ?? 0} credits; {entry.skill} costs {cost}.");
                return;
            }

            player.SetProperty(entry.aug, 1);
            creatureSkill.AdvancementClass = SkillAdvancementClass.Specialized;
            creatureSkill.InitLevel = 10;
            creatureSkill.Ranks = (ushort)Player.CalcSkillRank(SkillAdvancementClass.Specialized, creatureSkill.ExperienceSpent);
            player.AvailableSkillCredits -= cost;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, creatureSkill));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
            player.SendMessage($"Your {entry.skill} is now specialized.");
            CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}'s {entry.skill} specialized for {cost} credits; {player.AvailableSkillCredits} left.");
        }

        // the free augmentations for a tradesman: (property, most levels, attribute for an innate aug)
        private static readonly Dictionary<string, (PropertyInt prop, int max, PropertyAttribute? attr)> GrantableAugs = new Dictionary<string, (PropertyInt, int, PropertyAttribute?)>
        {
            ["joat"] = (PropertyInt.AugmentationJackOfAllTrades, 1, null),           // Jack of All Trades: +5 all skills
            ["charmedsmith"] = (PropertyInt.AugmentationBonusImbueChance, 1, null),  // Charmed Smith: +5% imbue chance
            ["world"] = (PropertyInt.LumAugAllSkills, 10, null),                     // Aura of the World: +1 all skills per level
            ["spec"] = (PropertyInt.LumAugSkilledSpec, 5, null),                     // Aura of Specialization: +2 specialized skills per level
            ["craft"] = (PropertyInt.LumAugSkilledCraft, 5, null),                   // Aura of the Craftsman: +1 crafting rolls per level
            ["str"] = (PropertyInt.AugmentationInnateStrength, 10, PropertyAttribute.Strength),
            ["end"] = (PropertyInt.AugmentationInnateEndurance, 10, PropertyAttribute.Endurance),
            ["coord"] = (PropertyInt.AugmentationInnateCoordination, 10, PropertyAttribute.Coordination),
            ["quick"] = (PropertyInt.AugmentationInnateQuickness, 10, PropertyAttribute.Quickness),
            ["focus"] = (PropertyInt.AugmentationInnateFocus, 10, PropertyAttribute.Focus),
            ["self"] = (PropertyInt.AugmentationInnateSelf, 10, PropertyAttribute.Self),
        };

        /// <summary>
        /// Project ACX: grant augmentations free (JJ 2026-11-24, for Brokk the tinker, who levels on passup and will never
        /// reach luminance: "lets make an assumption and get him the no cost augs to increase the tinker skills", then the
        /// attribute augs, not Ciandra's Fortune). Sets the property to the given level (most: the retail cap). An innate
        /// attribute aug works as the gem does: +5 innate per level, capped at 100, counted in the innate family (10 at most).
        /// </summary>
        [CommandHandler("acxgrantaug", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Grants a logged-in character an augmentation free, up to the given count (default its cap).",
            "<character name>, <joat | charmedsmith | world | spec | craft | str | end | coord | quick | focus | self>[, count]")]
        public static void HandleGrantAug(Session session, params string[] parameters)
        {
            var parts = string.Join(" ", parameters).Split(',');
            if (parts.Length < 2 || !GrantableAugs.TryGetValue(parts[1].Trim().ToLower(), out var aug))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: acxgrantaug <character name>, <joat | charmedsmith | world | spec | craft | str | end | coord | quick | focus | self>[, count]");
                return;
            }

            var player = PlayerManager.GetOnlinePlayer(parts[0].Trim());
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{parts[0].Trim()} isn't logged in.");
                return;
            }

            var target = parts.Length > 2 && int.TryParse(parts[2].Trim(), out var n) ? System.Math.Min(n, aug.max) : aug.max;
            var current = player.GetProperty(aug.prop) ?? 0;
            var added = 0;

            if (aug.attr == null)
            {
                if (target > current)
                {
                    player.SetProperty(aug.prop, target);
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, aug.prop, target));
                    added = target - current;
                }
            }
            else
            {
                var attribute = player.Attributes[aug.attr.Value];
                while (current + added < target && player.AugmentationInnateFamily < 10 && attribute.StartingValue < 100)
                {
                    attribute.StartingValue += System.Math.Min(5, 100 - attribute.StartingValue);
                    player.AugmentationInnateFamily++;
                    added++;
                }
                if (added > 0)
                {
                    player.SetProperty(aug.prop, current + added);
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, aug.prop, current + added));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AugmentationInnateFamily, player.AugmentationInnateFamily));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attribute));
                }
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name}: {parts[1].Trim()} +{added} (now {player.GetProperty(aug.prop) ?? 0}){(aug.attr != null ? $", {aug.attr} innate {player.Attributes[aug.attr.Value].StartingValue}, innate family {player.AugmentationInnateFamily}/10" : "")}.");
        }

        /// <summary>
        /// Project ACX: put an item in a logged-in character's pack from the console (JJ 2026-11-24: Brokk's Empowered Empyrean
        /// Robe, normally from the Saving Asheron raid, "Lets do #2"). Attuned and bonded as the item's own data says.
        /// </summary>
        [CommandHandler("acxgiveitem", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Creates an item in a logged-in character's pack.",
            "<character name>, <wcid>[, count]")]
        public static void HandleGiveItem(Session session, params string[] parameters)
        {
            var parts = string.Join(" ", parameters).Split(',');
            if (parts.Length < 2 || !uint.TryParse(parts[1].Trim(), out var wcid))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: acxgiveitem <character name>, <wcid>[, count]");
                return;
            }
            var player = PlayerManager.GetOnlinePlayer(parts[0].Trim());
            if (player == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{parts[0].Trim()} isn't logged in.");
                return;
            }
            var item = ACE.Server.Factories.WorldObjectFactory.CreateNewWorldObject(wcid);
            if (item == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"There's no item {wcid}.");
                return;
            }
            if (parts.Length > 2 && int.TryParse(parts[2].Trim(), out var count) && count > 1 && (item.MaxStackSize ?? 1) > 1)
                item.SetStackSize(System.Math.Min(count, item.MaxStackSize ?? 1));

            if (!player.TryCreateInInventoryWithNetworking(item))
            {
                item.Destroy();
                CommandHandlerHelper.WriteOutputInfo(session, $"{player.Name} has no room for {item.Name}.");
                return;
            }
            CommandHandlerHelper.WriteOutputInfo(session, $"Gave {player.Name} {item.Name}.");
        }
    }
}
