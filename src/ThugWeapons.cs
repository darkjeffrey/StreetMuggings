using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace StreetMuggings
{
    /// <summary>Swaps a freshly spawned thug's weapons for one blunt weapon that fits their tier.</summary>
    internal static class ThugWeapons
    {
        // Item ids from SandBoxCore/ModuleData/items/weapons.xml (Mace / TwoHandedMace templates).
        private static readonly Dictionary<ThugTier, string[]> ByTier = new Dictionary<ThugTier, string[]>
        {
            { ThugTier.Looter, new[] { "peasant_hammer_2_t1", "peasant_hammer_1_t1" } },              // wooden hammer, blacksmith hammer
            { ThugTier.Thug,   new[] { "battania_mace_1_t2", "aserai_mace_1_t2", "peasant_hammer_1_t1" } }, // knobbed club, militia mace
            { ThugTier.Bandit, new[] { "pernach_mace_t3", "light_mace_t3", "peasant_maul_t1" } },      // militia pernach, light mace, makeshift maul
            { ThugTier.Chief,  new[] { "steel_mace_t3", "peasant_maul_t1_2", "bone_crusher_mace_t3" } }  // steel shestopyor, maul, bone crusher
        };

        public static void GiveBluntWeapon(Agent agent, ThugTier tier)
        {
            ItemObject item = Pick(tier);
            if (item == null || agent == null || !agent.IsActive()) return; // keep their own weapons if no mace is loaded

            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumPrimaryWeaponSlots; slot++)
            {
                if (!agent.Equipment[slot].IsEmpty)
                    agent.RemoveEquippedWeapon(slot);
            }

            var weapon = new MissionWeapon(item, null, null);
            agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon0, ref weapon);
            agent.TryToWieldWeaponInSlot(EquipmentIndex.Weapon0, Agent.WeaponWieldActionType.InstantAfterPickUp, false);
        }

        private static ItemObject Pick(ThugTier tier)
        {
            var found = new List<ItemObject>();
            for (ThugTier t = tier; t >= ThugTier.Looter && found.Count == 0; t--)
            {
                foreach (string id in ByTier[t])
                {
                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (item != null) found.Add(item);
                }
            }
            return found.Count == 0 ? null : found[MBRandom.RandomInt(found.Count)];
        }
    }
}
