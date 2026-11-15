using System;

using ACE.Entity;
using ACE.Entity.Models;
namespace ACE.Server.WorldObjects
{
    public class Coin : Stackable
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Coin(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Coin(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            // Project ACX: coins made before the stack size went up (25,000 to 50,000) take the new size
            var weenieMaxStackSize = Database.DatabaseManager.World.GetCachedWeenie(WeenieClassId)?.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxStackSize);
            if (weenieMaxStackSize > (MaxStackSize ?? 0))
                MaxStackSize = (ushort)weenieMaxStackSize;
        }        
    }
}
