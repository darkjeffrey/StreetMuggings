using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace StreetMuggings
{
    /// <summary>The "Street Muggings" page in Mod Options (MCM). Saved to Documents\Mount and Blade II Bannerlord\Configs\ModSettings.</summary>
    public sealed class StreetMuggingsMcmSettings : AttributeGlobalSettings<StreetMuggingsMcmSettings>
    {
        public override string Id => "StreetMuggings";
        public override string DisplayName => "Street Muggings";
        public override string FolderName => "StreetMuggings";
        public override string FormatType => "json2";

        // ---------------- General ----------------
        private const string General = "General";

        [SettingPropertyBool("Enable muggings", Order = 0, RequireRestart = false, HintText = "Turn the whole mod on or off.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("Mugged when trading", Order = 1, RequireRestart = false, HintText = "Choosing Trade in a town can get you ambushed.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool TriggerOnTrade { get; set; } = true;

        [SettingPropertyBool("Mugged going to the tavern district", Order = 2, RequireRestart = false, HintText = "Choosing Go to the tavern district can get you ambushed.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool TriggerOnTavern { get; set; } = true;

        [SettingPropertyBool("Mugged on night walks", Order = 3, RequireRestart = false, HintText = "Taking a walk around the town center at night can get you jumped in the streets.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool TriggerOnNightStroll { get; set; } = true;

        [SettingPropertyBool("Allow daytime muggings", Order = 4, RequireRestart = false,
            HintText = "Off: you can only be mugged at night. On: Trade and the tavern district can also get you mugged during the day (using the 'by day' multiplier). Town walks stay night-only either way.")]
        [SettingPropertyGroup(General, GroupOrder = 0)]
        public bool AllowDaytimeMuggings { get; set; } = false;

        // ---------------- Mugging rate ----------------
        private const string Rate = "Mugging Rate";

        [SettingPropertyFloatingInteger("Overall rate multiplier", 0f, 5f, "0.00x", Order = 0, RequireRestart = false,
            HintText = "Multiplies every mugging chance. 1.00x is the default balance, 0.5x halves it, 2x doubles it.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float RateMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Base chance", 0f, 1f, "0%", Order = 1, RequireRestart = false,
            HintText = "Starting chance before town security, prosperity and time of day are applied. A town with 50 security and 4000 prosperity gives about this value.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float BaseChance { get; set; } = 0.12f;

        [SettingPropertyFloatingInteger("Maximum chance", 0f, 1f, "0%", Order = 2, RequireRestart = false,
            HintText = "No action can ever be riskier than this, however lawless or rich the town.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float MaxChance { get; set; } = 0.60f;

        [SettingPropertyFloatingInteger("Trade/tavern by day", 0f, 3f, "0.00x", Order = 3, RequireRestart = false,
            HintText = "Chance multiplier for trading or visiting the tavern during the day. Only used when Allow daytime muggings is on.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float TradeTavernDayMultiplier { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Trade/tavern at night", 0f, 3f, "0.00x", Order = 4, RequireRestart = false,
            HintText = "Chance multiplier for trading or visiting the tavern at night.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float TradeTavernNightMultiplier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Night walk", 0f, 3f, "0.00x", Order = 5, RequireRestart = false,
            HintText = "Chance multiplier for walking around the town center at night.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float NightStrollMultiplier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Gang leaders hate you (-100)", 1f, 5f, "0.00x", Order = 6, RequireRestart = false,
            HintText = "Chance multiplier when your average relation with the town's gang leaders is -100. It scales smoothly to 1x at 0 relation.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float HostileGangMultiplier { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Gang leaders like you (+100)", 0f, 1f, "0.00x", Order = 7, RequireRestart = false,
            HintText = "Chance multiplier when your average relation with the town's gang leaders is +100. It scales smoothly to 1x at 0 relation. Towns with no gang leaders are unaffected.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float FriendlyGangMultiplier { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Cooldown per town (hours)", 0f, 168f, "0", Order = 8, RequireRestart = false,
            HintText = "After a mugging, the same town leaves you alone for this many in-game hours.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public float CooldownHours { get; set; } = 24f;

        [SettingPropertyInteger("Minimum gold to be a target", 0, 5000, "0", Order = 9, RequireRestart = false,
            HintText = "Thugs ignore you if you carry less than this many denars.")]
        [SettingPropertyGroup(Rate, GroupOrder = 1)]
        public int MinGold { get; set; } = 50;

        // ---------------- Night walks ----------------
        private const string Walks = "Night Walks";

        [SettingPropertyFloatingInteger("Earliest ambush (seconds)", 0f, 300f, "0", Order = 0, RequireRestart = false,
            HintText = "The thugs strike at a random moment between the earliest and latest time after you start walking, and only once you're at least 20 m from where you came in.")]
        [SettingPropertyGroup(Walks, GroupOrder = 2)]
        public float MinWalkDelay { get; set; } = 30f;

        [SettingPropertyFloatingInteger("Latest ambush (seconds)", 0f, 600f, "0", Order = 1, RequireRestart = false,
            HintText = "The thugs strike at a random moment between the earliest and latest time after you start walking.")]
        [SettingPropertyGroup(Walks, GroupOrder = 2)]
        public float MaxWalkDelay { get; set; } = 120f;

        [SettingPropertyFloatingInteger("No guards nearby", 0f, 3f, "0.00x", Order = 2, RequireRestart = false,
            HintText = "Chance multiplier when no guards are near you at that moment.")]
        [SettingPropertyGroup(Walks, GroupOrder = 2)]
        public float NoGuardsMultiplier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Each nearby guard", 0f, 1f, "0.00x", Order = 3, RequireRestart = false,
            HintText = "Every guard near you multiplies the chance by this again. With the defaults: 0 guards 1.5x, 1 guard 0.9x, 2 guards 0.54x, 3 guards 0.32x.")]
        [SettingPropertyGroup(Walks, GroupOrder = 2)]
        public float PerGuardMultiplier { get; set; } = 0.6f;

        [SettingPropertyFloatingInteger("Guard counting radius (m)", 5f, 80f, "0", Order = 4, RequireRestart = false,
            HintText = "Guards within this distance of you count as nearby.")]
        [SettingPropertyGroup(Walks, GroupOrder = 2)]
        public float GuardCountRadius { get; set; } = 30f;

        // ---------------- Gang & stakes ----------------
        private const string Stakes = "Gang and Stakes";

        [SettingPropertyInteger("Smallest gang", 1, 10, "0", Order = 0, RequireRestart = false, HintText = "Gang size in the safest, poorest towns.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public int MinThugs { get; set; } = 2;

        [SettingPropertyInteger("Largest gang", 1, 15, "0", Order = 1, RequireRestart = false, HintText = "Gang size in the most lawless, richest towns.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public int MaxThugs { get; set; } = 7;

        [SettingPropertyFloatingInteger("Gold lost on defeat (min)", 0f, 1f, "0%", Order = 2, RequireRestart = false, HintText = "Share of your gold taken in an easy town if you lose the fight.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public float MinLossPercent { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Gold lost on defeat (max)", 0f, 1f, "0%", Order = 3, RequireRestart = false, HintText = "Share of your gold taken in the roughest towns if you lose the fight.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public float MaxLossPercent { get; set; } = 0.50f;

        [SettingPropertyBool("Allow paying them off", Order = 4, RequireRestart = false, HintText = "Muggers offer to let you hand over part of your purse instead of fighting.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public bool AllowPayOff { get; set; } = true;

        [SettingPropertyFloatingInteger("Pay-off size", 0f, 1f, "0%", Order = 5, RequireRestart = false, HintText = "The pay-off as a share of what you would lose by losing the fight.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public float PayOffFactor { get; set; } = 0.6f;

        [SettingPropertyBool("Thugs use blunt weapons", Order = 6, RequireRestart = false, HintText = "Muggers carry clubs, hammers and maces (better ones in rougher towns) instead of their usual weapons. Blunt hits knock people out rather than kill.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public bool BluntWeapons { get; set; } = true;

        [SettingPropertyBool("Muggers wear hoods", Order = 6, RequireRestart = false, HintText = "Muggers swap their headgear for a hood or head wrap (blackened hoods, or ones that fit the town's culture).")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public bool HoodedThugs { get; set; } = true;

        [SettingPropertyBool("Guards come to help", Order = 7, RequireRestart = false, HintText = "Town guards who can see you being attacked join the fight on your side.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public bool GuardsHelp { get; set; } = true;

        [SettingPropertyFloatingInteger("Guard sight range (m)", 5f, 60f, "0", Order = 8, RequireRestart = false, HintText = "How far away a guard can notice the fight. Guards within 8 m notice it even when facing away; farther ones must be looking your way with nothing blocking the view.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public float GuardSightRange { get; set; } = 25f;

        [SettingPropertyFloatingInteger("Security gained on victory", 0f, 10f, "0.0", Order = 9, RequireRestart = false, HintText = "Town security added when you beat the gang.")]
        [SettingPropertyGroup(Stakes, GroupOrder = 3)]
        public float SecurityGainOnWin { get; set; } = 1f;
    }
}
