using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: Smart Salvage (JJ 2026-11-11, ported from ClassicACE). With it on (/smartsalvage on), the Ust skips
    /// materials by a whitelist or blacklist, can leave inscribed items alone, and keeps salvage in workmanship bands
    /// (under 4, 4 to under 8, 8 and up) so good and poor salvage don't mix in one bag.
    /// </summary>
    partial class Player
    {
        public bool AcxSmartSalvageOn => GetProperty(PropertyBool.AcxSmartSalvage) ?? false;

        public List<MaterialType> AcxSalvageFilter()
        {
            return (GetProperty(PropertyString.AcxSalvageFilter) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var m) ? (MaterialType?)m : null).Where(m => m != null).Select(m => m.Value).ToList();
        }

        public void AcxSetSalvageFilter(IEnumerable<MaterialType> materials)
        {
            var list = materials.Distinct().Select(m => ((int)m).ToString()).ToList();
            if (list.Count == 0)
                RemoveProperty(PropertyString.AcxSalvageFilter);
            else
                SetProperty(PropertyString.AcxSalvageFilter, string.Join(",", list));
        }

        /// <summary>
        /// Called from HandleSalvaging: true if Smart Salvage leaves this item alone
        /// </summary>
        private bool AcxSalvageSkips(WorldObject item)
        {
            if (!AcxSmartSalvageOn)
                return false;

            if ((GetProperty(PropertyBool.AcxSalvageAvoidInscribed) ?? false) && !string.IsNullOrEmpty(item.Inscription))
                return true;

            var filter = AcxSalvageFilter();
            if (filter.Count == 0 || item.MaterialType == null)
                return false;

            var listed = filter.Contains(item.MaterialType.Value);
            return (GetProperty(PropertyBool.AcxSalvageWhitelist) ?? false) ? !listed : listed;
        }

        private static int AcxWorkmanshipBand(WorldObject item)
        {
            var workmanship = item.Workmanship ?? 0;
            return workmanship < 4 ? 0 : workmanship < 8 ? 1 : 2;
        }

        private readonly Dictionary<WorldObject, int> acxSalvageBands = new();

        /// <summary>
        /// GetSalvageBag, but a bag only takes salvage from its own workmanship band
        /// </summary>
        private WorldObject AcxGetBandedSalvageBag(MaterialType materialType, List<WorldObject> salvageBags, WorldObject item)
        {
            var band = AcxWorkmanshipBand(item);
            var existing = salvageBags.FirstOrDefault(i => (i.GetProperty(PropertyInt.MaterialType) ?? 0) == (int)materialType
                && (i.Structure ?? 0) < (i.MaxStructure ?? 0) && acxSalvageBands.TryGetValue(i, out var b) && b == band);
            if (existing != null)
                return existing;

            // a new bag: GetSalvageBag creates one when none of this material has room, so hide the other bands' bags from it
            var others = salvageBags.Where(i => (i.GetProperty(PropertyInt.MaterialType) ?? 0) == (int)materialType).ToList();
            var bag = GetSalvageBag(materialType, salvageBags.Except(others).ToList());
            if (!salvageBags.Contains(bag))
                salvageBags.Add(bag);
            acxSalvageBands[bag] = band;
            return bag;
        }
    }
}
