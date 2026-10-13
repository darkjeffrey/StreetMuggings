using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;

namespace StreetMuggings
{
    // Town menu: "Trade"
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_town_town_market_on_consequence")]
    internal static class TradePatch
    {
        private static bool Prefix() => MuggingManager.OnTownMenuAction(MuggingTrigger.Trade);
    }

    // Town menu: "Take a walk around the town center" (only rolls at night)
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_town_town_streets_on_consequence")]
    internal static class StrollPatch
    {
        private static bool Prefix() => MuggingManager.OnTownMenuAction(MuggingTrigger.NightStroll);
    }

    // Town menu: "Go to the tavern district". Its consequence is an anonymous lambda, so instead of patching
    // that by its compiler-generated name we catch the option itself as the menu runs it.
    [HarmonyPatch(typeof(GameMenuOption), nameof(GameMenuOption.RunConsequence))]
    internal static class TavernDistrictPatch
    {
        private static bool Prefix(GameMenuOption __instance, MenuContext menuContext)
        {
            if (__instance?.IdString != "town_backstreet") return true;
            if (menuContext?.GameMenu?.StringId != "town") return true;

            return MuggingManager.OnTownMenuAction(MuggingTrigger.Tavern);
        }
    }
}
