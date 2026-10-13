using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace StreetMuggings
{
    /// <summary>Gives muggers a hood or head wrap that suits the town they work in.</summary>
    internal static class ThugOutfits
    {
        // The Blackened Hood is the classic cutpurse look; every gang has a good share of them.
        private const string BlackHood = "assassin_hood";
        private const float BlackHoodShare = 0.4f;

        // Item ids from SandBoxCore/ModuleData/items/head_armors.xml.
        private static readonly Dictionary<string, string[]> ByCulture = new Dictionary<string, string[]>
        {
            { "aserai",   new[] { "aserai_civil_hscarf_b", "aserai_civil_hscarf_a", "tied_head_wrapping" } },
            { "darshi",   new[] { "aserai_civil_hscarf_b", "aserai_civil_hscarf_a", "tied_head_wrapping" } },
            { "khuzait",  new[] { "fur_hood", "nomad_padded_hood", "peaked_fur_hood" } },
            { "battania", new[] { "battania_civil_hood", "wrapped_headcloth" } },
            { "sturgia",  new[] { "fur_hood", "pilgrim_hood", "wrapped_headcloth" } },
            { "nord",     new[] { "fur_hood", "pilgrim_hood", "wrapped_headcloth" } },
            { "vlandia",  new[] { "pilgrim_hood", "cloth_headwrap" } },
            { "empire",   new[] { "pilgrim_hood", "cloth_headwrap" } }
        };
        private static readonly string[] Default = { "pilgrim_hood", "wrapped_headcloth", "cloth_headwrap" };

        /// <summary>Replaces whatever is on the thug's head with a hood (keeps the outfit unchanged if none is loaded).</summary>
        public static void PutOnHood(Equipment outfit, string cultureId)
        {
            if (outfit == null) return;

            ItemObject hood = null;
            if (MBRandom.RandomFloat < BlackHoodShare)
                hood = MBObjectManager.Instance.GetObject<ItemObject>(BlackHood);

            if (hood == null)
            {
                string[] pool = cultureId != null && ByCulture.TryGetValue(cultureId, out string[] p) ? p : Default;
                var loaded = new List<ItemObject>();
                foreach (string id in pool)
                {
                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (item != null) loaded.Add(item);
                }
                if (loaded.Count > 0) hood = loaded[MBRandom.RandomInt(loaded.Count)];
            }

            if (hood == null) hood = MBObjectManager.Instance.GetObject<ItemObject>(BlackHood);
            if (hood == null) return;

            outfit[EquipmentIndex.Head] = new EquipmentElement(hood);
        }
    }
}
