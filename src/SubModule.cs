using System;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Towns;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace StreetMuggings
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _harmony = new Harmony("darkjeffrey.streetmuggings");
            _harmony.PatchAll(typeof(SubModule).Assembly);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            MuggingManager.Reset();
        }

        public override void OnGameEnd(Game game)
        {
            MuggingManager.Reset();
            base.OnGameEnd(game);
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            MuggingContext pending = MuggingManager.Pending;
            if (pending == null) return;
            MuggingManager.Pending = null; // one shot, whatever scene this turns out to be

            try
            {
                if (Campaign.Current == null) return;
                if (Settlement.CurrentSettlement != pending.Settlement) return;
                if (mission.GetMissionBehavior<TownCenterMissionController>() == null) return;

                mission.AddMissionBehavior(new MuggingMissionLogic(pending));
            }
            catch (Exception e)
            {
                InformationManager.DisplayMessage(new InformationMessage("[StreetMuggings] " + e.Message, Colors.Red));
            }
        }
    }
}
