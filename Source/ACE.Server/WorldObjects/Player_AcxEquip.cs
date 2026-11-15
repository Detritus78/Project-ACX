using System.Linq;

using ACE.Entity.Models;

using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: auto-unequip off-hand and /fso (JJ 2026-11-11, ported from Pourkarutide's ClassicACE fork).
    /// Wielding a weapon from your pack that can't share hands with what's in your off-hand moves the off-hand item to
    /// your pack first: any off-hand for a two-hander, a heavy shield (mass over 140) or any non-shield off-hand for a bow
    /// or caster, anything but a shield for a thrown weapon. /fso clears an off-hand icon the client shows as still
    /// wielded after the server took the item off.
    /// </summary>
    partial class Player
    {
        private bool acxFixOffhandPending;

        public bool AcxHasIncompatibleOffhand(WorldObject itemToEquip, EquipMask wieldedLocation)
        {
            if (wieldedLocation != EquipMask.MeleeWeapon && wieldedLocation != EquipMask.TwoHanded && wieldedLocation != EquipMask.MissileWeapon && wieldedLocation != EquipMask.Held)
                return false;

            var offhand = GetEquippedOffHand();
            if (offhand == null || offhand == itemToEquip)
                return false;

            return itemToEquip.IsTwoHanded
                || (offhand.IsShield && (offhand.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.Mass) ?? 0) > 140 && (itemToEquip.IsCaster || itemToEquip.IsAmmoLauncher))
                || (!offhand.IsShield && (itemToEquip.IsCaster || itemToEquip.IsAmmoLauncher))
                || (itemToEquip.IsThrownWeapon && !offhand.IsShield);
        }

        /// <summary>
        /// Called from HandleActionGetAndWieldItem for an item in the pack: takes off a conflicting off-hand, then wields
        /// </summary>
        public void AcxWieldAfterOffhand(System.Action wield)
        {
            var chain = new ActionChain();
            chain.AddAction(this, () => AcxUnequip(EquipMask.Shield));
            chain.AddDelaySeconds(0.1);
            chain.AddAction(this, wield);
            chain.EnqueueChain();
        }

        private bool AcxUnequip(EquipMask location)
        {
            var item = EquippedObjects.Values.FirstOrDefault(e => e.CurrentWieldedLocation == location);
            if (item == null || !TryDequipObjectWithNetworking(item.Guid, out var dequipped, DequipObjectAction.DequipToPack))
                return false;

            if (!TryCreateInInventoryWithNetworking(dequipped))
            {
                if (!TryEquipObjectWithNetworking(dequipped, location))
                    log.Warn($"0x{dequipped.Guid}:{dequipped.Name} for player {Name} lost from AcxUnequip failure.");
                return false;
            }

            if (!acxFixOffhandPending)
            {
                acxFixOffhandPending = true;
                var chain = new ActionChain();
                chain.AddDelaySeconds(0.25);
                chain.AddAction(this, () => AcxFixStuckEquippedIcon(location));
                chain.EnqueueChain();
            }
            return true;
        }

        /// <summary>
        /// The client sometimes keeps an item's icon in a wield slot after the server unwields it. Wielding and removing a
        /// throwaway placeholder there clears it without a relog.
        /// </summary>
        public void AcxFixStuckEquippedIcon(EquipMask location)
        {
            if (EquippedObjects.Values.Any(e => e.CurrentWieldedLocation == location))
            {
                acxFixOffhandPending = false;
                return;
            }

            var placeholder = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.placeholder);
            if (placeholder == null)
            {
                acxFixOffhandPending = false;
                return;
            }
            placeholder.IconId = 0x06000F6C;   // the shield slot icon, so the brief flash draws little attention
            Session.Network.EnqueueSend(new GameMessageCreateObject(placeholder));

            var chain = new ActionChain();
            chain.AddDelaySeconds(0.25);
            chain.AddAction(this, () =>
            {
                if (!EquippedObjects.Values.Any(e => e.CurrentWieldedLocation == location))
                    Session.Network.EnqueueSend(new GameEventWieldItem(Session, placeholder.Guid.Full, location));
            });
            chain.AddDelaySeconds(0.1);
            chain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageDeleteObject(placeholder));
                placeholder.Destroy();
                acxFixOffhandPending = false;
            });
            chain.EnqueueChain();
        }

        /// <summary>
        /// Project ACX: equipped-item activation re-check (JJ 2026-11-11, ported from ClassicACE). Called from
        /// ManaConsumersTick for each worn item (every ~5 s): an item whose requirements (Arcane Lore, skills, level,
        /// attributes, heritage) stop being met switches its spells off, and one that couldn't activate switches on once
        /// they're met. Returns true to skip the item's mana use this tick.
        /// </summary>
        private bool AcxRecheckItem(WorldObject item)
        {
            if (item.ItemCurMana == null || item.ItemMaxMana == null)
                return !item.IsAffecting;

            // the requirement check also tests (and complains about) use cooldowns: leave items on cooldown alone
            if (item.CooldownId != null && !EnchantmentManager.CheckCooldown(item.CooldownId))
                return !item.IsAffecting;

            var met = item.CheckUseRequirements(this, true).Success;   // silent: an unsent error message would break AC:Unreal's game events

            if (!item.IsAffecting)
            {
                if (met && item.ItemCurMana > 1 && item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock).Count > 0 && TryActivateSpells(item) && item.IsAffecting)
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You now meet the requirements to activate the {item.Name}.", ChatMessageType.Magic));
                return true;
            }

            if (!met)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You no longer meet the requirements to activate the {item.Name}.", ChatMessageType.Magic));
                foreach (var spellId in item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock))
                    RemoveItemSpell(item, (uint)spellId);
                item.OnSpellsDeactivated();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Project ACX: world broadcasts come with a chime and ClassicACE's screen flash (JJ 2026-11-11), so they're
        /// hard to miss. Called from PlayerManager.BroadcastToAll.
        /// </summary>
        public void AcxBroadcastCue()
        {
            Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.UI_GeneralQuery, 1.0f));

            var chain = new ActionChain();
            for (var i = 0; i < 3; i++)
            {
                chain.AddAction(this, () => ApplyVisualEffects(PlayScript.VisionUpWhite));
                chain.AddDelaySeconds(1);
                chain.AddAction(this, () => ApplyVisualEffects(PlayScript.VisionDownBlack));
                chain.AddDelaySeconds(1);
            }
            chain.EnqueueChain();
        }
    }
}
