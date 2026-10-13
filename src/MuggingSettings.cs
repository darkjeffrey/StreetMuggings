using System;

namespace StreetMuggings
{
    /// <summary>
    /// Read-only view of the current settings. Values come from the MCM page
    /// (StreetMuggingsMcmSettings); the defaults here are only used if MCM isn't available.
    /// </summary>
    public static class MuggingSettings
    {
        private static readonly StreetMuggingsMcmSettings Defaults = new StreetMuggingsMcmSettings();

        private static StreetMuggingsMcmSettings S
        {
            get
            {
                try { return StreetMuggingsMcmSettings.Instance ?? Defaults; }
                catch (Exception) { return Defaults; }
            }
        }

        public static bool Enabled => S.Enabled;
        public static bool TriggerOnTrade => S.TriggerOnTrade;
        public static bool TriggerOnTavern => S.TriggerOnTavern;
        public static bool TriggerOnNightStroll => S.TriggerOnNightStroll;
        public static bool AllowDaytimeMuggings => S.AllowDaytimeMuggings;

        public static float RateMultiplier => Math.Max(0f, S.RateMultiplier);
        public static float BaseChance => Math.Max(0f, S.BaseChance);
        public static float MaxChance => Math.Max(0f, Math.Min(1f, S.MaxChance));
        public static float TradeTavernDayMultiplier => Math.Max(0f, S.TradeTavernDayMultiplier);
        public static float TradeTavernNightMultiplier => Math.Max(0f, S.TradeTavernNightMultiplier);
        public static float NightStrollMultiplier => Math.Max(0f, S.NightStrollMultiplier);
        public static float HostileGangMultiplier => Math.Max(0f, S.HostileGangMultiplier);
        public static float FriendlyGangMultiplier => Math.Max(0f, S.FriendlyGangMultiplier);

        // Prosperity at (or above) which a town counts as "fully rich" for scaling.
        public static float ProsperityForMax => 8000f;

        public static float CooldownHours => Math.Max(0f, S.CooldownHours);

        public static float MinWalkDelay => Math.Max(0f, Math.Min(S.MinWalkDelay, S.MaxWalkDelay));
        public static float MaxWalkDelay => Math.Max(0f, Math.Max(S.MinWalkDelay, S.MaxWalkDelay));
        public static float NoGuardsMultiplier => Math.Max(0f, S.NoGuardsMultiplier);
        public static float PerGuardMultiplier => Math.Max(0f, Math.Min(1f, S.PerGuardMultiplier));
        public static float GuardCountRadius => Math.Max(1f, S.GuardCountRadius);
        public static int MinGold => Math.Max(0, S.MinGold);

        public static int MinThugs => Math.Max(1, Math.Min(S.MinThugs, S.MaxThugs));
        public static int MaxThugs => Math.Max(1, Math.Max(S.MinThugs, S.MaxThugs));

        public static float MinLossPercent => Math.Min(S.MinLossPercent, S.MaxLossPercent);
        public static float MaxLossPercent => Math.Max(S.MinLossPercent, S.MaxLossPercent);

        public static bool AllowPayOff => S.AllowPayOff;
        public static bool BluntWeapons => S.BluntWeapons;
        public static bool HoodedThugs => S.HoodedThugs;
        public static bool GuardsHelp => S.GuardsHelp;
        public static float GuardSightRange => Math.Max(1f, S.GuardSightRange);
        public static float PayOffFactor => S.PayOffFactor;
        public static float SecurityGainOnWin => S.SecurityGainOnWin;

        // ---------------- Debug (not in the mod menu) ----------------
        // Flip these to true and rebuild when testing. Keep them false for releases.

        /// <summary>Every eligible Trade, tavern-district visit and town walk triggers a mugging, day or night,
        /// ignoring chance, cooldown and minimum gold.</summary>
        public static bool AlwaysMug = false;

        /// <summary>Print the town's stats, the computed chance and the roll each time an action is checked.</summary>
        public static bool DebugMessages = false;
    }
}
