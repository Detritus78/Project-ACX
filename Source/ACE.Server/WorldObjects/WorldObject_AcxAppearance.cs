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
        /// Sends the aura to one client. AC:Unreal queues it until the item's actor exists.
        /// </summary>
        public void SendAcxAura(Session session)
        {
            var script = AcxAuraScript;
            if (script == null || session == null)
                return;

            for (var i = 0; i < AcxAuraScriptCount; i++)
                session.Network.EnqueueSend(new GameMessagePlayScriptId(Guid, script.Value));
        }
    }
}
