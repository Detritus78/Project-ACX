using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: lets ordinary items send their own texture swaps (biota texture map).
    /// ACE only sends those for creatures and corpses, so item texture swaps never reached the client.
    /// </summary>
    partial class WorldObject
    {
        /// <summary>
        /// When true, this item's own texture map is applied on top of its ClothingBase look
        /// </summary>
        public bool AcxCustomAppearance
        {
            get => GetProperty(PropertyBool.AcxCustomAppearance) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.AcxCustomAppearance); else SetProperty(PropertyBool.AcxCustomAppearance, value); }
        }

        private ACE.Entity.ObjDesc ApplyAcxAppearance(ACE.Entity.ObjDesc objDesc)
        {
            if (!AcxCustomAppearance || this is Creature)
                return objDesc;

            var swaps = Biota.PropertiesTextureMap.Clone(BiotaDatabaseLock);
            if (swaps == null)
                return objDesc;

            foreach (var swap in swaps)
            {
                // the ClothingBase may already swap this texture: retarget that change instead of adding one the client would never match
                var existing = objDesc.TextureChanges.FirstOrDefault(t => t.PartIndex == swap.PartIndex && t.OldTexture == swap.OldTexture);
                if (existing != null)
                    existing.NewTexture = swap.NewTexture;
                else
                    objDesc.TextureChanges.Add(swap.Clone());
            }

            return objDesc;
        }

        /// <summary>
        /// Adds or replaces a texture swap on this item (used by /acxtex)
        /// </summary>
        public void SetAcxTextureSwap(byte partIndex, uint oldTexture, uint newTexture)
        {
            var swaps = Biota.PropertiesTextureMap.Clone(BiotaDatabaseLock) ?? new List<PropertiesTextureMap>();
            swaps.RemoveAll(t => t.PartIndex == partIndex && t.OldTexture == oldTexture);
            swaps.Add(new PropertiesTextureMap { PartIndex = partIndex, OldTexture = oldTexture, NewTexture = newTexture });

            Biota.PropertiesTextureMap = swaps;
            AcxCustomAppearance = true;
            ChangesDetected = true;
        }

        public void ClearAcxTextureSwaps()
        {
            Biota.PropertiesTextureMap = null;
            AcxCustomAppearance = false;
            ChangesDetected = true;
        }

        /// <summary>
        /// PhysicsScript (0x33xxxxxx) played on this item whenever a client is sent it, e.g. nether smoke
        /// </summary>
        public uint? AcxAuraScript
        {
            get => GetProperty(PropertyDataId.AcxAuraScript);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.AcxAuraScript); else SetProperty(PropertyDataId.AcxAuraScript, value.Value); }
        }

        /// <summary>
        /// How many copies of the aura script to stack (denser smoke)
        /// </summary>
        public int AcxAuraScriptCount
        {
            get => GetProperty(PropertyInt.AcxAuraScriptCount) ?? 1;
            set { if (value <= 1) RemoveProperty(PropertyInt.AcxAuraScriptCount); else SetProperty(PropertyInt.AcxAuraScriptCount, value); }
        }

        /// <summary>
        /// A second PhysicsScript played on top of the aura, e.g. the Nether Baton's sparks
        /// </summary>
        public uint? AcxAuraScript2
        {
            get => GetProperty(PropertyDataId.AcxAuraScript2);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.AcxAuraScript2); else SetProperty(PropertyDataId.AcxAuraScript2, value.Value); }
        }

        /// <summary>
        /// How many copies of the second script to stack
        /// </summary>
        public int AcxAuraScript2Count
        {
            get => GetProperty(PropertyInt.AcxAuraScript2Count) ?? 1;
            set { if (value <= 1) RemoveProperty(PropertyInt.AcxAuraScript2Count); else SetProperty(PropertyInt.AcxAuraScript2Count, value); }
        }

        /// <summary>
        /// A third PhysicsScript played once, e.g. the purple sparks at a nether thrown weapon's grip
        /// </summary>
        public uint? AcxAuraScript3
        {
            get => GetProperty(PropertyDataId.AcxAuraScript3);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.AcxAuraScript3); else SetProperty(PropertyDataId.AcxAuraScript3, value.Value); }
        }

        /// <summary>
        /// Every PhysicsScript play the aura takes, in order, copies included
        /// </summary>
        public IEnumerable<uint> AcxAuraPlays()
        {
            if (AcxAuraScript is uint script)
            {
                for (var i = 0; i < AcxAuraScriptCount; i++)
                    yield return script;
            }

            if (AcxAuraScript2 is uint script2)
            {
                for (var i = 0; i < AcxAuraScript2Count; i++)
                    yield return script2;
            }

            if (AcxAuraScript3 is uint script3)
                yield return script3;
        }

        /// <summary>
        /// Sends the aura to one client. AC:Unreal queues it until the item's actor exists.
        /// </summary>
        public void SendAcxAura(Session session)
        {
            if (session == null)
                return;

            foreach (var script in AcxAuraPlays())
                session.Network.EnqueueSend(new GameMessagePlayScriptId(Guid, script));
        }

        /// <summary>
        /// Plays the item's effects again for everyone who sees its wielder: its model's own (e.g. a Flaming Javelin's
        /// flames) and the aura. A thrown weapon is hidden at each throw and put back in the hand after the reload, and
        /// hiding it clears its effects on the client (JJ 2026-11-15: "after you throw it once it looses its colors and
        /// animations").
        /// </summary>
        public void BroadcastAcxEffects(WorldObject wielder)
        {
            var defaultScript = CSetup?.DefaultScript ?? 0;
            if (defaultScript != 0)
                wielder.EnqueueBroadcast(new GameMessagePlayScriptId(Guid, defaultScript));

            foreach (var script in AcxAuraPlays())
                wielder.EnqueueBroadcast(new GameMessagePlayScriptId(Guid, script));
        }
    }
}
