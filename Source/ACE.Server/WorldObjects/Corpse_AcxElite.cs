using System.Collections.Generic;

using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: an elite invader's corpse (Managers/AcxInvasions.cs) is open to anyone who reaches it before it rots, not
    /// only the killer (JJ 2026-11-13: "Not other players fault someone gets there first and kills it in a second"), lasts
    /// acx_invasion_corpse_minutes, and carries a stack of ACX Marks from which each player may take one (Managers/AcxMarks.cs)
    /// </summary>
    partial class Corpse
    {
        /// <summary>
        /// Players who have taken their ACX Mark from this corpse; null on every other corpse
        /// </summary>
        public HashSet<uint> AcxMarkTakers;

        public bool AcxOpenToAll => AcxMarkTakers != null;

        /// <summary>
        /// Called from Creature.CreateCorpse for an elite
        /// </summary>
        public void AcxMakeEliteCorpse()
        {
            AcxMarkTakers = new HashSet<uint>();
            TimeToRot = PropertyManager.GetLong("acx_invasion_corpse_minutes").Item * 60;

            var marks = AcxMarks.Create((int)PropertyManager.GetLong("acx_invasion_corpse_marks").Item);
            if (marks != null && !TryAddToInventory(marks))
                marks.Destroy();
        }
    }
}
