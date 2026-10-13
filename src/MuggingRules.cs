using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace StreetMuggings
{
    public enum MuggingTrigger
    {
        Trade,
        Tavern,
        NightStroll
    }

    public enum ThugTier
    {
        Looter = 0,
        Thug = 1,
        Bandit = 2,
        Chief = 3
    }

    /// <summary>Everything about one planned mugging, decided on the campaign map before the scene loads.</summary>
    public sealed class MuggingContext
    {
        public Settlement Settlement;
        public MuggingTrigger Trigger;
        public float Insecurity;   // 0 = perfectly safe town, 1 = lawless
        public float Wealth;       // 0 = poor town, 1 = very prosperous
        public float Difficulty;   // 0..1, drives thug count and quality
        public float SpringDelay;  // seconds after the player spawns before the thugs appear
        public float WalkChance;   // night walks: chance rolled in the streets, before the nearby-guard modifier
        public bool Forced;        // "Always get mugged" was on when this was planned
        public List<CharacterObject> Thugs = new List<CharacterObject>();
        public List<ThugTier> Tiers = new List<ThugTier>();
    }

    public static class MuggingRules
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static float Insecurity(Town town) => Clamp01(1f - town.Security / 100f);

        public static float Wealth(Town town) => Clamp01(town.Prosperity / MuggingSettings.ProsperityForMax);

        /// <summary>Lower security and higher prosperity both raise the odds.</summary>
        public static float Chance(Town town, MuggingTrigger trigger, bool isNight)
        {
            float insecurity = Insecurity(town);
            float wealth = Wealth(town);

            float insecurityFactor = 0.25f + 1.75f * insecurity; // 0.25 .. 2.0
            float wealthFactor = 0.5f + wealth;                   // 0.5  .. 1.5

            float triggerMult;
            if (trigger == MuggingTrigger.NightStroll)
                triggerMult = MuggingSettings.NightStrollMultiplier;
            else
                triggerMult = isNight ? MuggingSettings.TradeTavernNightMultiplier : MuggingSettings.TradeTavernDayMultiplier;

            float chance = MuggingSettings.BaseChance * insecurityFactor * wealthFactor * triggerMult * MuggingSettings.RateMultiplier
                           * GangRelationFactor(town.Settlement);
            return Math.Max(0f, Math.Min(MuggingSettings.MaxChance, chance));
        }

        /// <summary>Lawless towns breed bolder gangs; rich towns attract better-armed ones.</summary>
        public static float Difficulty(Town town) => Clamp01(0.55f * Insecurity(town) + 0.45f * Wealth(town));

        /// <summary>
        /// Average relation with the town's gang leaders: at -100 the gangs set their men on you
        /// (default 2x), at +100 they keep them off you (default 0.25x), at 0 nothing changes.
        /// Towns without gang leaders give 1x.
        /// </summary>
        public static float GangRelationFactor(Settlement settlement)
        {
            float relation = AverageGangLeaderRelation(settlement, out int leaders);
            if (leaders == 0) return 1f;

            float t = Clamp01(Math.Abs(relation) / 100f);
            float atExtreme = relation < 0f ? MuggingSettings.HostileGangMultiplier : MuggingSettings.FriendlyGangMultiplier;
            if (atExtreme <= 0f) return 0f;
            return (float)Math.Pow(atExtreme, t); // smooth curve through 1x at 0 relation
        }

        public static float AverageGangLeaderRelation(Settlement settlement, out int leaders)
        {
            leaders = 0;
            float total = 0f;
            if (settlement?.Notables == null || Hero.MainHero == null) return 0f;
            foreach (Hero notable in settlement.Notables)
            {
                if (notable == null || !notable.IsAlive || !notable.IsGangLeader) continue;
                total += notable.GetRelationWithPlayer();
                leaders++;
            }
            return leaders == 0 ? 0f : total / leaders;
        }

        /// <summary>No guards nearby makes muggers bolder; every guard in sight makes them more careful.</summary>
        public static float GuardFactor(int nearbyGuards)
            => MuggingSettings.NoGuardsMultiplier * (float)Math.Pow(MuggingSettings.PerGuardMultiplier, Math.Max(0, nearbyGuards));

        public static float LossFraction(float difficulty)
            => MuggingSettings.MinLossPercent + (MuggingSettings.MaxLossPercent - MuggingSettings.MinLossPercent) * Clamp01(difficulty);

        public static int GoldLostOnDefeat(float difficulty)
            => (int)Math.Round(Hero.MainHero.Gold * LossFraction(difficulty));

        public static int PayOffAmount(float difficulty)
        {
            int amount = (int)Math.Round(Hero.MainHero.Gold * LossFraction(difficulty) * MuggingSettings.PayOffFactor);
            return Math.Min(Hero.MainHero.Gold, Math.Max(10, amount));
        }

        public static MuggingContext Build(Settlement settlement, MuggingTrigger trigger)
        {
            Town town = settlement.Town;
            var ctx = new MuggingContext
            {
                Settlement = settlement,
                Trigger = trigger,
                Insecurity = Insecurity(town),
                Wealth = Wealth(town),
                Difficulty = Difficulty(town),
                SpringDelay = trigger == MuggingTrigger.NightStroll
                    ? MBRandom.RandomFloatRanged(MuggingSettings.MinWalkDelay, MuggingSettings.MaxWalkDelay)
                    : MBRandom.RandomFloatRanged(1.0f, 2.0f)
            };

            float d = ctx.Difficulty;
            int span = MuggingSettings.MaxThugs - MuggingSettings.MinThugs;
            int count = (int)Math.Round(MuggingSettings.MinThugs + d * span + MBRandom.RandomFloatRanged(-0.5f, 0.5f));
            count = Math.Max(MuggingSettings.MinThugs, Math.Min(MuggingSettings.MaxThugs, count));

            ThugPool pool = ThugPool.For(town);
            for (int i = 0; i < count; i++)
            {
                ThugTier tier;
                if (i == 0 && d >= 0.75f)
                {
                    tier = ThugTier.Chief; // a proper gang boss leads the worst crews
                }
                else
                {
                    float roll = d * 2f + MBRandom.RandomFloatRanged(-0.6f, 0.6f);
                    tier = (ThugTier)Math.Max(0, Math.Min(2, (int)Math.Round(roll)));
                }

                CharacterObject character = pool.Get(tier);
                if (character == null) continue;
                ctx.Thugs.Add(character);
                ctx.Tiers.Add(tier);
            }
            return ctx;
        }

        public static int LootFor(ThugTier tier, float wealth)
        {
            int lo, hi;
            switch (tier)
            {
                case ThugTier.Chief: lo = 80; hi = 150; break;
                case ThugTier.Bandit: lo = 30; hi = 70; break;
                case ThugTier.Thug: lo = 20; hi = 50; break;
                default: lo = 10; hi = 25; break;
            }
            return (int)Math.Round(MBRandom.RandomInt(lo, hi + 1) * (0.6f + 0.8f * wealth));
        }
    }

    /// <summary>Picks thug troop types from what the game actually has loaded, with fallbacks.</summary>
    internal sealed class ThugPool
    {
        private CharacterObject _looter, _thug, _bandit, _chief;

        public static ThugPool For(Town town)
        {
            MBObjectManager om = MBObjectManager.Instance;
            CultureObject townCulture = town.Culture;

            CharacterObject looter = om.GetObject<CultureObject>("looters")?.BasicTroop
                                     ?? om.GetObject<CharacterObject>("looter");

            CharacterObject thug = townCulture?.GangleaderBodyguard ?? looter;

            CultureObject banditCulture = om.GetObject<CultureObject>(BanditCultureFor(townCulture?.StringId));
            CharacterObject bandit = banditCulture?.BanditRaider ?? banditCulture?.BanditBandit ?? thug;
            CharacterObject chief = banditCulture?.BanditChief ?? bandit;

            looter = looter ?? thug ?? townCulture?.Townsman;

            return new ThugPool { _looter = looter, _thug = thug ?? looter, _bandit = bandit ?? thug ?? looter, _chief = chief ?? bandit ?? thug ?? looter };
        }

        public CharacterObject Get(ThugTier tier)
        {
            switch (tier)
            {
                case ThugTier.Chief: return _chief;
                case ThugTier.Bandit: return _bandit;
                case ThugTier.Thug: return _thug;
                default: return _looter;
            }
        }

        private static string BanditCultureFor(string cultureId)
        {
            switch (cultureId)
            {
                case "battania": return "forest_bandits";
                case "sturgia":
                case "nord": return "sea_raiders";
                case "khuzait": return "steppe_bandits";
                case "aserai":
                case "darshi": return "desert_bandits";
                default: return "mountain_bandits"; // empire, vlandia, anything modded
            }
        }
    }
}
