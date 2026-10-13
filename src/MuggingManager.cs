using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace StreetMuggings
{
    /// <summary>
    /// Campaign-side flow: decides whether a town-menu action turns into a mugging,
    /// shows the "your purse or your life" choice, and hands the plan to the scene.
    /// </summary>
    public static class MuggingManager
    {
        /// <summary>The mugging waiting for the next town-center scene to load.</summary>
        public static MuggingContext Pending;

        // Last mugging time per town (hours since campaign start). Not saved: a reload simply resets cooldowns.
        private static readonly Dictionary<string, double> LastMuggingHour = new Dictionary<string, double>();

        public static void Reset()
        {
            Pending = null;
            LastMuggingHour.Clear();
        }

        /// <summary>Harmony prefix body. Returns true to let the original menu action run.</summary>
        public static bool OnTownMenuAction(MuggingTrigger trigger)
        {
            try
            {
                Settlement settlement = Settlement.CurrentSettlement;
                bool isNight = CampaignTime.Now.IsNightTime;
                if (!IsEligible(settlement, trigger, isNight)) return true;

                bool forced = MuggingSettings.AlwaysMug;

                if (trigger == MuggingTrigger.NightStroll)
                {
                    // Night walks are rolled later, in the streets, once we can see how many guards are about.
                    MuggingContext walk = MuggingRules.Build(settlement, trigger);
                    if (walk.Thugs.Count == 0) return true;
                    walk.WalkChance = MuggingRules.Chance(settlement.Town, trigger, isNight);
                    walk.Forced = forced;
                    if (MuggingSettings.DebugMessages)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[StreetMuggings] Night walk in {settlement.Name}: security {settlement.Town.Security:0}, prosperity {settlement.Town.Prosperity:0}, gangs {GangInfo(settlement)}, base chance {walk.WalkChance:P1}{(forced ? " (always mug)" : "")}, rolled in {walk.SpringDelay:0}s",
                            Colors.Gray));
                    }
                    Pending = walk;
                    return true;
                }

                float chance = forced ? 1f : MuggingRules.Chance(settlement.Town, trigger, isNight);
                float roll = MBRandom.RandomFloat;
                if (MuggingSettings.DebugMessages)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[StreetMuggings] {trigger} in {settlement.Name}: security {settlement.Town.Security:0}, prosperity {settlement.Town.Prosperity:0}, gangs {GangInfo(settlement)}, chance {chance:P1}{(forced ? " (always mug)" : "")}, roll {roll:0.000}",
                        Colors.Gray));
                }
                if (!forced && roll >= chance) return true;

                MuggingContext ctx = MuggingRules.Build(settlement, trigger);
                if (ctx.Thugs.Count == 0) return true;

                MarkMugged(settlement);
                // The gang is placed in the town-center scene and the pay-or-fight choice is made there.
                OpenAmbushScene(ctx);
                return false;
            }
            catch (Exception e)
            {
                InformationManager.DisplayMessage(new InformationMessage("[StreetMuggings] error: " + e.Message, Colors.Red));
                return true;
            }
        }

        private static string GangInfo(Settlement settlement)
        {
            float relation = MuggingRules.AverageGangLeaderRelation(settlement, out int leaders);
            return leaders == 0 ? "none (x1.00)" : $"{leaders} leader(s), avg relation {relation:0} (x{MuggingRules.GangRelationFactor(settlement):0.00})";
        }

        /// <summary>Starts this town's cooldown.</summary>
        public static void MarkMugged(Settlement settlement)
        {
            if (settlement != null) LastMuggingHour[settlement.StringId] = CampaignTime.Now.ToHours;
        }

        private static bool IsEligible(Settlement settlement, MuggingTrigger trigger, bool isNight)
        {
            if (!MuggingSettings.Enabled) return false;
            if (settlement == null || !settlement.IsTown || settlement.Town == null) return false;
            if (settlement.IsUnderSiege) return false;
            if (Campaign.Current == null || Campaign.Current.IsMainHeroDisguised) return false;
            if (PlayerEncounter.LocationEncounter == null) return false;
            if (Hero.MainHero == null) return false;

            switch (trigger)
            {
                case MuggingTrigger.Trade: if (!MuggingSettings.TriggerOnTrade) return false; break;
                case MuggingTrigger.Tavern: if (!MuggingSettings.TriggerOnTavern) return false; break;
                case MuggingTrigger.NightStroll: if (!MuggingSettings.TriggerOnNightStroll) return false; break;
            }

            // Testing switch: skip the time-of-day, purse and cooldown rules.
            if (MuggingSettings.AlwaysMug) return true;

            if (!isNight && (trigger == MuggingTrigger.NightStroll || !MuggingSettings.AllowDaytimeMuggings)) return false;
            if (Hero.MainHero.Gold < MuggingSettings.MinGold) return false;

            if (LastMuggingHour.TryGetValue(settlement.StringId, out double last)
                && CampaignTime.Now.ToHours - last < MuggingSettings.CooldownHours)
                return false;

            return true;
        }

        private static void OpenAmbushScene(MuggingContext ctx)
        {
            try
            {
                Location center = LocationComplex.Current?.GetLocationWithId("center");
                if (center == null || PlayerEncounter.LocationEncounter == null) return;

                Pending = ctx;
                PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(center, null, null, null); // MuggingMissionLogic moves you to the market / tavern door
            }
            catch (Exception e)
            {
                Pending = null;
                InformationManager.DisplayMessage(new InformationMessage("[StreetMuggings] could not start the fight: " + e.Message, Colors.Red));
            }
        }
    }
}
