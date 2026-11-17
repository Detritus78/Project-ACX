using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the Alchemist Trainer and the Thrown Weapons Trainer by Holtburg's lifestone (acx/Content/newplayer/
    /// 20-trainers.sql; JJ 2026-11-16: "I havent figured out how to tell people about the alchemy class before they create a
    /// char. We may need to create a npc in the starter building that gives players an opt to switch right there to thrown or
    /// alchemy", then "put the trainers in holtburg by the lifestone").
    /// A fresh start, once per character and shared by both trainers (JJ 2026-11-17: "I think they should dump all your skills
    /// and direct you to reskill and redo your attributes in concordia ... port you to the reskill area automatically? They
    /// could ask you.. Do you want to try alchemy? ... only one time", then "It would specialize alchemy for them though"):
    /// after a Yes/No popup every trained and specialized skill is reset with its credits and experience refunded (as at a
    /// temple, Player.AcxResetAllSkills), the trainer's own skill (Alchemy or Missile Weapons) is trained and specialized,
    /// and the character is sent to the gem rooms beside Concordia's lifestone to rebuild the rest with the Forgetfulness,
    /// Enlightenment, Lowering and Raising gems. Only up to unlimited_respec_max_level (the level the gems stop needing their
    /// hourly wait), so the rebuild takes minutes, not hours.
    /// Each trainer also hands over a starter weapon once per character: a Concussion Bomb whose debuff is tier 1's (a physical
    /// one, JJ 2026-11-17: "make it a physical like bludgeon"), or an Eternal Javelin, both worthless to vendors. A character
    /// with Alchemy specialized some other way (at creation, or with the gems) finds the same starter bomb in its pack at its
    /// next login (OnLogin; JJ: "Yes add a starter bomb").
    /// </summary>
    public static class AcxTrainers
    {
        public const uint AlchemistTrainer = 900901, ThrownTrainer = 900902;

        // the vendors' plain Eternal Javelin (thrown/20-vendor-eternal.sql)
        private const uint StarterJavelin = 900815;

        private const string FreshStartQuest = "AcxTrainerFreshStart";

        private const string StarterBombQuest = "AcxTrainerStarterBomb", StarterJavelinQuest = "AcxTrainerStarterJavelin";

        // the west gem room beside Concordia's lifestone, where and facing as JJ stood (/loc 2026-11-17; the gem generator's own
        // row, 0x018A020D [82.37 -105.08], has a wrong cell and left him in portal space)
        private static readonly Position GemRoom = new Position(0x018A0230, 80.728584f, -100.081978f, 0.006f, 0f, 0f, 0.999958f, 0.009150f);

        private static readonly string[] AlchemistPitch =
        {
            "Strength? Keep it. A bomb hits as hard as your Alchemy lets it.",
            "One throw and your target and everything around it take the blast, then they're all left weak to what's inside. And a good bomb never runs out.",
        };

        private static readonly string[] ThrownPitch =
        {
            "A good javelin always comes home. Throw it, catch it, throw it again.",
            "All you need is Missile Weapons and a strong arm. Keep your shield if you like; you'll just throw a little slower.",
        };

        public static bool IsTrainer(WorldObject wo) => wo.WeenieClassId == AlchemistTrainer || wo.WeenieClassId == ThrownTrainer;

        private static int MaxLevel => (int)PropertyManager.GetLong("unlimited_respec_max_level").Item;

        /// <summary>
        /// Called from Creature.ActOnUse when a player talks to a trainer
        /// </summary>
        public static void OnTrainerUse(Creature trainer, Player player)
        {
            if (player.IsOlthoiPlayer)
                return;

            var alchemist = trainer.WeenieClassId == AlchemistTrainer;
            var target = alchemist ? Skill.Alchemy : Skill.MissileWeapons;

            if (player.GetCreatureSkill(target).AdvancementClass == SkillAdvancementClass.Specialized)
            {
                Tell(trainer, player, alchemist ? "One of us already, I see." : "Your arm's already trained. Go throw something.");
                GiveStarter(trainer, player, alchemist);
                return;
            }

            foreach (var line in alchemist ? AlchemistPitch : ThrownPitch)
                Tell(trainer, player, line);

            if (!CanStartFresh(trainer, player))
                return;

            var question = $"Want to try {(alchemist ? "the Alchemist" : "thrown weapons")}? I'll clear every skill you've trained and give back the credits and experience, "
                + $"specialize {target.ToSentence()} for you, and send you to Concordia to rebuild the rest of your skills and attributes. You only get this once.";

            player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () => FreshStart(trainer, player, target, alchemist)), question);
        }

        private static bool CanStartFresh(Creature trainer, Player player)
        {
            if (player.QuestManager.HasQuest(FreshStartQuest))
            {
                Tell(trainer, player, "You've had your fresh start already. The gems beside Concordia's lifestone can still help you.");
                return false;
            }
            if (player.Level > MaxLevel)
            {
                Tell(trainer, player, "You've come too far to start over with me. The gems beside Concordia's lifestone can still help you.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// The player said Yes: checked again, since the popup may have waited
        /// </summary>
        private static void FreshStart(Creature trainer, Player player, Skill target, bool alchemist)
        {
            if (player.GetCreatureSkill(target).AdvancementClass == SkillAdvancementClass.Specialized || !CanStartFresh(trainer, player))
                return;

            player.AcxResetAllSkills();

            var skill = player.GetCreatureSkill(target);
            var skillBase = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)target];
            if (skill.AdvancementClass < SkillAdvancementClass.Trained)
                player.TrainSkill(target, skillBase.TrainedCost);
            player.SpecializeSkill(target, skillBase.UpgradeCostFromTrainedToSpecialized, false);

            player.QuestManager.Stamp(FreshStartQuest);

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, skill),
                new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0),
                new GameMessageSystemChat($"Your skills are reset, their credits and experience returned, and {target.ToSentence()} is specialized.", ChatMessageType.Advancement));

            Tell(trainer, player, alchemist ? "It's done. Welcome to the craft. Now go finish yourself in Concordia." : "It's done. Now go finish yourself in Concordia, and make it count.");
            GiveStarter(trainer, player, alchemist);

            var travel = new ActionChain();
            travel.AddDelaySeconds(3);
            travel.AddAction(player, () =>
            {
                if (PlayerManager.GetOnlinePlayer(player.Guid) != null && !player.IsDead)
                    player.Teleport(new Position(GemRoom));
            });
            travel.EnqueueChain();
        }

        /// <summary>
        /// At login: an Alchemist without a starter bomb yet finds one in the pack, a few seconds in once the pack is loaded
        /// </summary>
        public static void OnLogin(Player player)
        {
            if (!AcxBombWcids.Enabled || player.IsOlthoiPlayer || player.QuestManager.HasQuest(StarterBombQuest))
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(6);
            actionChain.AddAction(player, () =>
            {
                if (PlayerManager.GetOnlinePlayer(player.Guid) == null || player.QuestManager.HasQuest(StarterBombQuest)
                    || player.GetCreatureSkill(Skill.Alchemy).AdvancementClass != SkillAdvancementClass.Specialized)
                    return;

                var bomb = CreateStarter(true);
                if (bomb == null)
                    return;
                if (!player.TryCreateInInventoryWithNetworking(bomb))
                {
                    bomb.Destroy();   // a full pack: next login
                    return;
                }
                player.QuestManager.Stamp(StarterBombQuest);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"A {bomb.Name} waits in your pack: every Alchemist starts with one.", ChatMessageType.Broadcast));
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Character creation (Factories/PlayerFactory.cs): Missile Weapons without Fletching opts for thrown weapons (JJ
        /// 2026-11-17: "if you select missile but no fletching then you are opting for thrown so you get a thrown weapon"),
        /// so the character gets a starter Eternal thrown weapon in place of its heritage's launcher and ammunition: one of
        /// the heritage's own kind, as retail gives each heritage its own launcher, worthless to vendors like the trainer's
        /// javelin, and it is the Thrown Weapons Trainer's gift. False when the character trained Fletching (a bow, then).
        /// </summary>
        public static bool GiveStarterThrownAtCreation(Player player, HeritageGroup heritage)
        {
            if (player.GetCreatureSkill(Skill.Fletching).AdvancementClass >= SkillAdvancementClass.Trained)
                return false;

            var wcid = heritage switch
            {
                HeritageGroup.Aluvian => 900816u,      // Throwing Axe
                HeritageGroup.Gharundim => 900814u,    // Djarid
                HeritageGroup.Sho => 900811u,          // Shouken
                HeritageGroup.Viamontian => 900812u,   // Throwing Dagger
                _ => StarterJavelin,
            };
            var item = WorldObjectFactory.CreateNewWorldObject(wcid);
            if (item == null)
                return false;

            item.Value = 0;
            if (!player.TryAddToInventory(item))
            {
                item.Destroy();
                return false;
            }
            player.QuestManager.Stamp(StarterJavelinQuest);
            return true;
        }

        private static WorldObject CreateStarter(bool bomb)
        {
            var item = WorldObjectFactory.CreateNewWorldObject(bomb ? AcxBombWcids.Concussion : StarterJavelin);
            if (item == null)
                return null;

            if (bomb)
                item.SetProperty(PropertyDataId.AcxBombTier, 1);   // a tier 1 bomb's debuff; an unrolled bomb would carry tier 8's
            item.Value = 0;
            return item;
        }

        /// <summary>
        /// The starter weapon, once per character from each trainer
        /// </summary>
        private static void GiveStarter(Creature trainer, Player player, bool alchemist)
        {
            var quest = alchemist ? StarterBombQuest : StarterJavelinQuest;
            if (player.QuestManager.HasQuest(quest))
                return;

            var item = CreateStarter(alchemist);
            if (item == null)
                return;

            if (!player.TryCreateInInventoryWithNetworking(item))
            {
                item.Destroy();
                Tell(trainer, player, $"Your pack's full. Come back for your {(alchemist ? "starter bomb" : "javelin")}.");
                return;
            }

            player.QuestManager.Stamp(quest);
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{trainer.Name} gives you {item.Name}.", ChatMessageType.Broadcast));
        }

        private static void Tell(Creature trainer, Player player, string text)
        {
            player.Session.Network.EnqueueSend(new GameEventTell(trainer, text, player, ChatMessageType.Tell));
        }
    }
}
