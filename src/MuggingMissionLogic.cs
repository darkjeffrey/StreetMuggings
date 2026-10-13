using System;
using System.Collections.Generic;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.Usables;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace StreetMuggings
{
    /// <summary>
    /// Added to the town-center scene when a mugging is pending. Waits for the player to
    /// spawn, puts the thugs around them and runs the fight through the game's own
    /// MissionFightHandler (the same system alley fights use).
    /// </summary>
    public class MuggingMissionLogic : MissionLogic
    {
        private enum State { Placing, Waiting, Presenting, Confronting, Fighting, Leaving, Posing, Done }

        private readonly MuggingContext _ctx;
        private State _state = State.Placing;
        private float _elapsed;
        private readonly List<Agent> _thugAgents = new List<Agent>();
        private MissionFightHandler _fightHandler;
        private bool _offeredPayOff;
        private float _guardCheckTimer;
        private Vec3 _startPosition = Vec3.Invalid;
        private readonly HashSet<Agent> _guardsJoined = new HashSet<Agent>();
        private float _stateTimer;

        // Where each kind of trader stands in the town center; the goods merchant is what "Trade" opens.
        private static readonly string[] MarketTags = { "sp_merchant", "sp_weaponsmith", "sp_armorer", "sp_blacksmith", "sp_horse_merchant" };

        public MuggingMissionLogic(MuggingContext ctx)
        {
            _ctx = ctx;
        }

        public override void OnMissionTick(float dt)
        {
            if (_state == State.Fighting)
            {
                _guardCheckTimer -= dt;
                if (_guardCheckTimer <= 0f)
                {
                    _guardCheckTimer = 0.5f;
                    try { CallInGuards(); } catch (Exception) { /* never let guard logic break the fight */ }
                }
                return;
            }
            if (_state == State.Leaving) { TickLeaving(dt); return; }
            if (_state == State.Posing) { TickPose(dt); return; }
            if (_state == State.Confronting || _state == State.Done) return;

            Agent main = Agent.Main;
            if (main == null || !main.IsActive()) return;

            if (_state == State.Presenting)
            {
                // Give the player a moment to see who is surrounding them before the choice pops up.
                _stateTimer -= dt;
                if (_stateTimer <= 0f) Confront(main);
                return;
            }

            if (_state == State.Placing)
            {
                // The scene always starts you at the town gate; move you to where you were heading.
                Vec3 placed = Vec3.Invalid;
                if (_ctx.Trigger == MuggingTrigger.Trade) placed = TryMovePlayerToMarket(main);
                else if (_ctx.Trigger == MuggingTrigger.Tavern) placed = TryMovePlayerToTavernDoor(main);
                _startPosition = placed.IsValid ? placed : main.Position;

                if (_ctx.Trigger != MuggingTrigger.NightStroll)
                {
                    // Trade/tavern: the gang is already waiting for you when the scene fades in.
                    _fightHandler = Mission.GetMissionBehavior<MissionFightHandler>();
                    if (_fightHandler == null) { _state = State.Done; return; }
                    try { Spring(main, _startPosition); }
                    catch (Exception e)
                    {
                        _state = State.Done;
                        InformationManager.DisplayMessage(new InformationMessage("[StreetMuggings] ambush failed: " + e.Message, Colors.Red));
                    }
                    return;
                }

                _state = State.Waiting;
                return;
            }

            _elapsed += dt;
            if (_elapsed < _ctx.SpringDelay) return;

            // Night walks start at the town gate: wait until the player has actually gone into the streets.
            if (_ctx.Trigger == MuggingTrigger.NightStroll && _startPosition.IsValid
                && main.Position.Distance(_startPosition) < MinDistanceFromStart) return;

            // Don't interrupt a conversation or another fight (e.g. an alley fight); just wait.
            if (Campaign.Current?.ConversationManager != null && Campaign.Current.ConversationManager.IsConversationFlowActive) return;
            _fightHandler = Mission.GetMissionBehavior<MissionFightHandler>();
            if (_fightHandler == null) { _state = State.Done; return; }
            if (_fightHandler.IsThereActiveFight()) return;

            if (_ctx.Trigger == MuggingTrigger.NightStroll && !RollNightWalk(main))
            {
                _state = State.Done; // the thugs didn't fancy their odds tonight
                return;
            }

            try
            {
                MuggingManager.MarkMugged(_ctx.Settlement);
                Spring(main, main.Position);
            }
            catch (Exception e)
            {
                _state = State.Done;
                InformationManager.DisplayMessage(new InformationMessage("[StreetMuggings] ambush failed: " + e.Message, Colors.Red));
            }
        }

        private Vec3 TryMovePlayerToMarket(Agent main)
        {
            try
            {
                MissionAgentHandler agentHandler = Mission.GetMissionBehavior<MissionAgentHandler>();
                if (agentHandler == null) return Vec3.Invalid;

                foreach (string tag in MarketTags)
                {
                    List<UsableMachine> points = agentHandler.GetAllUsablePointsWithTag(tag);
                    if (points == null || points.Count == 0) continue;

                    Vec3 stall = points[0].GameEntity.GlobalPosition;
                    Vec3 spot = FindSpawnSpots(stall, 1, 3f, 6f, 2f)[0];
                    if (spot.Distance(stall) > 0.5f)
                    {
                        main.TeleportToPosition(spot);
                        return spot;
                    }
                }
            }
            catch (Exception)
            {
                // No market found in this scene: you simply start at the usual spot.
            }
            return Vec3.Invalid;
        }

        private const float MinDistanceFromStart = 20f;

        /// <summary>Night walks: the chance is rolled here, lower with guards nearby and higher with none.</summary>
        private bool RollNightWalk(Agent main)
        {
            int guards = CountNearbyGuards(main.Position, MuggingSettings.GuardCountRadius);
            float factor = MuggingRules.GuardFactor(guards);
            float chance = Math.Min(MuggingSettings.MaxChance, _ctx.WalkChance * factor);
            float roll = MBRandom.RandomFloat;
            bool hit = _ctx.Forced || roll < chance;

            if (MuggingSettings.DebugMessages)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[StreetMuggings] Street roll: {guards} guard(s) within {MuggingSettings.GuardCountRadius:0} m (x{factor:0.00}), chance {chance:P1}{(_ctx.Forced ? " (always mug)" : "")}, roll {roll:0.000} -> {(hit ? "mugged" : "safe")}",
                    Colors.Gray));
            }
            return hit;
        }

        private int CountNearbyGuards(Vec3 around, float radius)
        {
            int count = 0;
            foreach (Agent agent in Mission.Agents)
            {
                if (!agent.IsHuman || !agent.IsActive()) continue;
                if (!(agent.Character is CharacterObject co)) continue;
                if (co.Occupation != Occupation.Guard && co.Occupation != Occupation.Soldier) continue;
                if (agent.Position.Distance(around) <= radius) count++;
            }
            return count;
        }

        private Vec3 TryMovePlayerToTavernDoor(Agent main)
        {
            try
            {
                MissionAgentHandler agentHandler = Mission.GetMissionBehavior<MissionAgentHandler>();
                if (agentHandler?.TownPassageProps == null) return Vec3.Invalid;

                foreach (UsableMachine machine in agentHandler.TownPassageProps)
                {
                    if (!(machine is Passage passage) || passage.ToLocation?.StringId != "tavern") continue;

                    // Same point the game uses when you walk out of the tavern.
                    Vec3 door = passage.PilotStandingPoint != null
                        ? passage.PilotStandingPoint.GameEntity.GlobalPosition
                        : passage.GameEntity.GlobalPosition;
                    Vec3 spot = FindSpawnSpots(door, 1, 1.5f, 4f, 1f)[0];
                    main.TeleportToPosition(spot);
                    return spot;
                }
            }
            catch (Exception)
            {
                // No tavern door found in this scene: you simply start at the usual spot.
            }
            return Vec3.Invalid;
        }

        /// <summary>Guards who can see the fight join in on the player's side.</summary>
        private void CallInGuards()
        {
            if (!MuggingSettings.GuardsHelp || _fightHandler == null || !_fightHandler.IsThereActiveFight()) return;
            Agent main = Agent.Main;
            if (main == null || !main.IsActive()) return;

            Vec3 victim = main.GetChestGlobalPosition();
            float range = MuggingSettings.GuardSightRange;

            foreach (Agent agent in Mission.Agents)
            {
                if (!agent.IsHuman || !agent.IsActive() || !agent.IsAIControlled) continue;
                if (_guardsJoined.Contains(agent) || _thugAgents.Contains(agent)) continue;
                if (!(agent.Character is CharacterObject co)) continue;
                if (co.Occupation != Occupation.Guard && co.Occupation != Occupation.Soldier) continue;

                Vec3 eye = agent.GetEyeGlobalPosition();
                float dist = eye.Distance(victim);
                if (dist > range) continue;

                // Close guards notice the noise; farther ones have to be looking roughly this way.
                Vec3 toVictim = (victim - eye).NormalizedCopy();
                bool facing = Vec3.DotProduct(agent.LookDirection, toVictim) > 0.2f;
                if (dist > 8f && !facing) continue;

                // Nothing solid (walls, houses) in between.
                bool blocked = Mission.Scene.RayCastForClosestEntityOrTerrain(eye, victim, out float hitDist, out Vec3 _, 0.01f, BodyFlags.AgentOnly)
                               && hitDist < dist - 0.5f;
                if (blocked) continue;

                _guardsJoined.Add(agent);
                _fightHandler.AddAgentToSide(agent, true);

                if (_guardsJoined.Count == 1)
                    MBInformationManager.AddQuickInformation(new TextObject("{=SM_Guards}Guards! Stop those thugs!"), 0, agent.Character, null, "");
            }
        }

        private void Spring(Agent main, Vec3 around)
        {
            MissionAgentHandler agentHandler = Mission.GetMissionBehavior<MissionAgentHandler>();
            if (agentHandler == null) { _state = State.Done; return; }

            if (MuggingSettings.PoseMode) { SpringPose(agentHandler, main); return; }

            List<Vec3> spots = FindSpawnSpots(around, _ctx.Thugs.Count, 5f, 11f, 3.5f);
            for (int i = 0; i < _ctx.Thugs.Count && i < spots.Count; i++)
            {
                Agent thug = SpawnThug(agentHandler, _ctx.Thugs[i], spots[i], around);
                if (thug == null) continue;
                if (MuggingSettings.BluntWeapons) ThugWeapons.GiveBluntWeapon(thug, _ctx.Tiers[i]);
                thug.SetLookAgent(main);
                _thugAgents.Add(thug);
            }

            if (_thugAgents.Count == 0) { _state = State.Done; return; }

            // Trade/tavern scenes are still fading in, so wait a little longer before asking.
            _stateTimer = _ctx.Trigger == MuggingTrigger.NightStroll ? 0.8f : 1.5f;
            _state = State.Presenting;
        }

        private void Confront(Agent main)
        {
            int payOff = MuggingRules.PayOffAmount(_ctx.Difficulty);
            if (!MuggingSettings.AllowPayOff || payOff <= 0)
            {
                StartFight(main);
                return;
            }

            _state = State.Confronting;
            _offeredPayOff = true;

            TextObject text;
            switch (_ctx.Trigger)
            {
                case MuggingTrigger.Trade:
                    text = new TextObject("{=SM_ConfrontTrade}Before you reach the stalls, {COUNT} rough-looking men step out and block your path. Their leader grins: \"Nice purse you've got there. Hand it over and nobody gets hurt.\"");
                    break;
                case MuggingTrigger.Tavern:
                    text = new TextObject("{=SM_ConfrontTavern}Just outside the tavern, {COUNT} men peel away from the wall and close in around you. \"Leaving so soon? Not with that purse, you're not.\"");
                    break;
                default:
                    text = new TextObject("{=SM_NightConfront}{COUNT} shadows peel away from the walls and close in around you. \"Out for a midnight stroll, are we? Purse. Now.\"");
                    break;
            }
            text.SetTextVariable("COUNT", _thugAgents.Count);
            var payText = new TextObject("{=SM_Pay}Hand over {AMOUNT} denars");
            payText.SetTextVariable("AMOUNT", payOff);

            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=SM_Title}Ambushed!").ToString(),
                text.ToString(),
                true,
                true,
                new TextObject("{=SM_Fight}Fight").ToString(),
                payText.ToString(),
                () =>
                {
                    Agent player = Agent.Main;
                    if (player != null && player.IsActive()) StartFight(player);
                    else _state = State.Done;
                },
                () => PayOff(payOff),
                "", 0f, null, null, null), true, false);
        }

        private void PayOff(int amount)
        {
            amount = Math.Min(amount, Hero.MainHero.Gold);
            if (amount > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, false);

            var msg = new TextObject("{=SM_Paid}The thugs snatch your purse and bolt off into the alleys.");
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Yellow));

            SendThugsAway();
        }

        private const float LeaveDistance = 35f;   // fade out once this far from the player...
        private const float LeaveMaxTime = 15f;    // ...or after this long, whichever comes first

        /// <summary>The gang runs off together, away from the player, before disappearing.</summary>
        private void SendThugsAway()
        {
            _thugAgents.RemoveAll(a => a == null || !a.IsActive());
            if (_thugAgents.Count == 0) { _state = State.Done; return; }

            Agent main = Agent.Main;
            Vec3 player = main != null ? main.Position : _startPosition;

            // Head for a walkable spot roughly opposite the player, as far as we can find.
            Vec3 gang = Vec3.Zero;
            foreach (Agent thug in _thugAgents) gang += thug.Position;
            gang = gang * (1f / _thugAgents.Count);
            Vec3 away = gang - player;
            away.z = 0f;
            away = away.LengthSquared > 0.01f ? away.NormalizedCopy() : Vec3.Forward;

            Vec3 exit = Vec3.Invalid;
            float best = -1f;
            for (int i = 0; i < 16; i++)
            {
                Vec3 p = Mission.GetRandomPositionAroundPoint(gang + away * 30f, 0f, 12f, false);
                if (!p.IsValid) continue;
                float score = p.Distance(player);
                if (score > best) { best = score; exit = p; }
            }
            if (!exit.IsValid) exit = gang + away * 30f;

            Team friendly = Mission.PlayerAllyTeam;

            foreach (Agent thug in _thugAgents)
            {
                thug.SetLookAgent(null);
                thug.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.WithAnimation);
                thug.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.WithAnimation);

                // They've been paid: off the enemy team, and no fight/flee/wander behaviors that could
                // send them back at you. Only then alarm them, which is what makes town agents run.
                if (friendly != null) thug.SetTeam(friendly, true);
                MakeDocile(thug);
                thug.SetWatchState(Agent.WatchState.Alarmed);
                Vec3 target = Mission.GetRandomPositionAroundPoint(exit, 0f, 3f, false);
                if (!target.IsValid) target = exit;
                _exitTargets[thug] = target;
                OrderToExit(thug, target);
            }

            _stateTimer = 0f;
            _reorderTimer = 0f;
            _state = State.Leaving;
        }

        private const Agent.AIScriptedFrameFlags RunFlags = Agent.AIScriptedFrameFlags.NoAttack | Agent.AIScriptedFrameFlags.NeverSlowDown;

        // Paid-off thug -> where he's running to. The navigator re-activates the alarmed behavior group a moment
        // after we alarm him, and activating it cancels scripted movement, so the order is re-issued every quarter second.
        private readonly Dictionary<Agent, Vec3> _exitTargets = new Dictionary<Agent, Vec3>();
        private float _reorderTimer;

        private void OrderToExit(Agent thug, Vec3 target)
        {
            var worldPos = new WorldPosition(Mission.Scene, target);
            AgentNavigator navigator = thug.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
            if (navigator != null)
                navigator.SetTargetFrame(worldPos, 0f, 1f, -10f, RunFlags, true);
            else
                thug.SetScriptedPosition(ref worldPos, false, RunFlags);
        }

        /// <summary>Strip the behaviors (fight, flee, wander) that could take over a paid-off thug.</summary>
        private static void MakeDocile(Agent thug)
        {
            AgentNavigator navigator = thug.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
            if (navigator == null) return;
            navigator.GetBehaviorGroup<AlarmedBehaviorGroup>()?.DisableAllBehaviors();
            navigator.GetBehaviorGroup<DailyBehaviorGroup>()?.DisableAllBehaviors();
        }

        private void TickLeaving(float dt)
        {
            _stateTimer += dt;
            Agent main = Agent.Main;

            _reorderTimer -= dt;
            bool reorder = _reorderTimer <= 0f;
            if (reorder) _reorderTimer = 0.25f;

            for (int i = _thugAgents.Count - 1; i >= 0; i--)
            {
                Agent thug = _thugAgents[i];
                if (thug == null || !thug.IsActive()) { _thugAgents.RemoveAt(i); continue; }

                bool farEnough = main == null || thug.Position.Distance(main.Position) > LeaveDistance;
                if (farEnough || _stateTimer > LeaveMaxTime)
                {
                    thug.FadeOut(false, true);
                    _thugAgents.RemoveAt(i);
                    _exitTargets.Remove(thug);
                    continue;
                }

                if (reorder && _exitTargets.TryGetValue(thug, out Vec3 target))
                {
                    try
                    {
                        // Keep him docile too: the alarmed group's behaviors can be re-enabled when it reactivates.
                        MakeDocile(thug);
                        OrderToExit(thug, target);
                    }
                    catch (Exception) { /* fading out on the timer still cleans up */ }
                }
            }

            if (_thugAgents.Count == 0) _state = State.Done;
        }

        private void StartFight(Agent main)
        {
            var playerSide = new List<Agent> { main };
            foreach (Agent companion in NearbyCompanions(main))
                playerSide.Add(companion);

            var opponents = new List<Agent>();
            foreach (Agent thug in _thugAgents)
                if (thug.IsActive()) opponents.Add(thug);
            if (opponents.Count == 0) { _state = State.Done; return; }

            _state = State.Fighting;
            _fightHandler.StartCustomFight(playerSide, opponents, false, false, OnFightEnd, 0f);

            TextObject shout;
            if (_offeredPayOff)
                shout = new TextObject("{=SM_ShoutRefused}Have it your way. Get him!");
            else if (_ctx.Trigger == MuggingTrigger.NightStroll)
                shout = new TextObject("{=SM_ShoutNight}Out for a midnight stroll, are we? Your purse. Now.");
            else
                shout = new TextObject("{=SM_ShoutDay}Your purse or your life, friend. Take him, lads!");
            MBInformationManager.AddQuickInformation(shout, 0, opponents[0].Character, null, "");
        }

        private Agent SpawnThug(MissionAgentHandler agentHandler, CharacterObject character, Vec3 position, Vec3 lookAt, bool burly = false)
        {
            Campaign.Current.Models.AgeModel.GetAgeLimitForLocation(character, out int minAge, out int maxAge, "AlleyGangMember");
            if (maxAge <= minAge) { minAge = 20; maxAge = 45; }

            Monster monster = TaleWorlds.Core.FaceGen.GetMonsterWithSuffix(character.Race, "_settlement");
            AgentData data = new AgentData(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor)))
                .Monster(monster)
                .NoHorses(true);

            // Pick one complete outfit and build the face from that same outfit. Left alone, the game mixes a random
            // outfit piece by piece but generates the face for a hooded set, so many thugs end up bald and hoodless.
            Equipment outfit = character.RandomBattleEquipment?.Clone(false);
            if (outfit != null)
            {
                if (MuggingSettings.HoodedThugs) ThugOutfits.PutOnHood(outfit, _ctx.Settlement?.Town?.Culture?.StringId);
                BodyProperties body = character.GetBodyProperties(outfit, MBRandom.RandomInt(1, int.MaxValue));
                if (burly)
                {
                    // Pose mode: the biggest, broadest build the face generator allows.
                    body = new BodyProperties(new DynamicBodyProperties(body.DynamicProperties.Age, 0.65f, 1f), body.StaticProperties);
                }
                data = data.Equipment(outfit).BodyProperties(body);
            }
            data = data.Age(MBRandom.RandomInt(minAge, maxAge));

            string actionSet = ActionSetCode.GenerateActionSetNameWithSuffix(data.AgentMonster, data.AgentIsFemale, "_villain");

            var locationCharacter = new LocationCharacter(
                data,
                BehaviorSets.AddFixedCharacterBehaviors, // gives the agent the AlarmedBehaviorGroup the fight handler needs
                null,
                true,
                LocationCharacter.CharacterRelations.Enemy,
                actionSet,
                false,   // use battle equipment: thugs come armed
                false,
                null,
                false,
                false,
                false,
                null,
                false);

            Vec3 facing = lookAt - position;
            facing.z = 0f;
            Mat3 rotation = Mat3.Identity;
            if (facing.LengthSquared > 0.0001f)
            {
                rotation.f = facing.NormalizedCopy();
                rotation.u = Vec3.Up;
                rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation = rotation;
            frame.origin = position;

            return agentHandler.SpawnWanderingAgentWithInitialFrame(locationCharacter, frame, WeakGameEntity.Invalid, true, false);
        }

        private List<Vec3> FindSpawnSpots(Vec3 center, int count, float minDist, float maxDist, float minFromCenter)
        {
            var result = new List<Vec3>();
            Scene scene = Mission.Scene;

            for (int attempt = 0; attempt < count * 12 && result.Count < count; attempt++)
            {
                Vec3 p = Mission.GetRandomPositionAroundPoint(center, minDist, maxDist, false);
                if (!p.IsValid || p.Distance(center) < minFromCenter) continue;

                var face = PathFaceRecord.NullFaceRecord;
                scene.GetNavMeshFaceIndex(ref face, p, true);
                if (!face.IsValid()) continue;

                bool tooClose = false;
                foreach (Vec3 other in result)
                    if (other.Distance(p) < 1.2f) { tooClose = true; break; }
                if (tooClose) continue;

                result.Add(p);
            }

            // Cramped street: reuse the spots we did find (slightly offset) rather than dropping thugs.
            if (result.Count == 0) result.Add(center);
            int found = result.Count;
            for (int i = 0; result.Count < count; i++)
            {
                Vec3 basePos = result[i % found];
                result.Add(new Vec3(basePos.x + 0.7f * (1 + i / found), basePos.y, basePos.z, -1f));
            }

            return result;
        }

        private IEnumerable<Agent> NearbyCompanions(Agent main)
        {
            foreach (Agent agent in Mission.Agents)
            {
                if (agent == main || !agent.IsHuman || !agent.IsActive()) continue;
                if (!(agent.Character is CharacterObject co) || co.HeroObject == null) continue;
                if (co.HeroObject.PartyBelongedTo != MobileParty.MainParty) continue;
                if (agent.Position.Distance(main.Position) > 25f) continue;
                yield return agent;
            }
        }

        private void OnFightEnd(bool isPlayerSideWon)
        {
            _state = State.Done;

            try
            {
                if (isPlayerSideWon) RewardVictory();
                else ApplyDefeat();
            }
            catch (Exception e)
            {
                InformationManager.DisplayMessage(new InformationMessage("[StreetMuggings] " + e.Message, Colors.Red));
            }
        }

        private void RewardVictory()
        {
            int loot = 0;
            foreach (ThugTier tier in _ctx.Tiers) loot += MuggingRules.LootFor(tier, _ctx.Wealth);
            if (loot > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, loot, true);

            Town town = _ctx.Settlement?.Town;
            if (town != null && MuggingSettings.SecurityGainOnWin > 0f)
                town.Security = Math.Min(100f, town.Security + MuggingSettings.SecurityGainOnWin);

            var msg = new TextObject("{=SM_Won}The would-be muggers lie in the dirt. You help yourself to their purses: {GOLD} denars.");
            msg.SetTextVariable("GOLD", loot);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
        }

        private void ApplyDefeat()
        {
            int lost = Math.Min(Hero.MainHero.Gold, MuggingRules.GoldLostOnDefeat(_ctx.Difficulty));
            if (lost > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, lost, true);

            var msg = new TextObject("{=SM_Lost}You come to in a gutter, head pounding. Your purse is {GOLD} denars lighter.");
            msg.SetTextVariable("GOLD", lost);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));

            // Same exit the game uses when you lose an alley fight.
            if (Agent.Main == null || !Agent.Main.IsActive())
            {
                Mission.Current.NextCheckTimeEndMission = 0f;
                if (!Campaign.Current.IsMainHeroDisguised)
                    Campaign.Current.GameMenuManager.SetNextMenu("settlement_player_unconscious");
            }
        }

        // ---------------- Pose mode (debug, see MuggingSettings.PoseMode) ----------------
        // Stages a group shot instead of a mugging: the gang lines up in a tight half-circle behind the player,
        // everyone facing the same way, weapons away, and holds still for as long as you like.

        private const int PoseGangSize = 5;
        private const float PoseRadius = 1.3f;        // how far behind the player the arc sits
        private const float PoseArcDegrees = 80f;     // each end of the arc, measured from straight behind
        private const float PoseReformDistance = 1.5f;

        private readonly Dictionary<Agent, Vec3> _poseSpots = new Dictionary<Agent, Vec3>();
        private Vec3 _poseAnchor = Vec3.Invalid;
        private Vec3 _poseForward = Vec3.Forward;
        private Vec3 _poseLastPlayerPos = Vec3.Invalid;
        private float _poseTimer;

        private void SpringPose(MissionAgentHandler agentHandler, Agent main)
        {
            if (_ctx.Thugs.Count == 0) { _state = State.Done; return; }
            Vec3 forward = Flat(main.LookDirection);
            List<Vec3> spots = PoseSpots(main.Position, forward);

            Team friendly = Mission.PlayerAllyTeam;
            for (int i = 0; i < spots.Count; i++)
            {
                CharacterObject character = _ctx.Thugs[i % _ctx.Thugs.Count];
                Agent thug = SpawnThug(agentHandler, character, spots[i], spots[i] + forward, true);
                if (thug == null) continue;

                thug.SetLookAgent(null);
                thug.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
                thug.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);
                if (friendly != null) thug.SetTeam(friendly, true);
                MakeDocile(thug);

                _thugAgents.Add(thug);
                _poseSpots[thug] = spots[i];
            }

            if (_thugAgents.Count == 0) { _state = State.Done; return; }

            _poseAnchor = main.Position;
            _poseForward = forward;
            _poseLastPlayerPos = main.Position;
            _poseTimer = 0f;
            _state = State.Posing;

            InformationManager.DisplayMessage(new InformationMessage(
                "[StreetMuggings] Pose mode: face away from the gang. To turn the whole group, walk a couple of steps the way you want to face and stop; they re-form behind you.",
                Colors.Gray));
        }

        /// <summary>Spots on a half-circle behind the player, pulled in toward the player if a wall is in the way.</summary>
        private List<Vec3> PoseSpots(Vec3 center, Vec3 forward)
        {
            var spots = new List<Vec3>();
            Vec3 back = -forward;
            Vec3 side = Vec3.CrossProduct(forward, Vec3.Up).NormalizedCopy();
            Scene scene = Mission.Scene;

            for (int i = 0; i < PoseGangSize; i++)
            {
                float t = PoseGangSize == 1 ? 0.5f : i / (float)(PoseGangSize - 1);
                float angle = (t * 2f - 1f) * PoseArcDegrees * (float)Math.PI / 180f;
                // The middle man stands a touch farther back so heads don't line up perfectly.
                float radius = PoseRadius + (Math.Abs(angle) < 0.3f ? 0.25f : 0f);
                Vec3 offset = back * ((float)Math.Cos(angle) * radius) + side * ((float)Math.Sin(angle) * radius);

                Vec3 chosen = Vec3.Invalid;
                for (float pull = 1f; pull >= 0.4f; pull -= 0.15f)
                {
                    Vec3 p = center + offset * pull;
                    var face = PathFaceRecord.NullFaceRecord;
                    scene.GetNavMeshFaceIndex(ref face, p, true);
                    if (face.IsValid()) { chosen = p; break; }
                }
                if (chosen.IsValid) spots.Add(chosen);
            }
            return spots;
        }

        private void TickPose(float dt)
        {
            _poseTimer -= dt;
            if (_poseTimer > 0f) return;
            _poseTimer = 0.5f;

            _thugAgents.RemoveAll(a => a == null || !a.IsActive());
            if (_thugAgents.Count == 0) { _state = State.Done; return; }

            Agent main = Agent.Main;
            if (main != null && main.IsActive())
            {
                // The player walked somewhere and stopped: re-form behind them, facing the way they walked.
                bool stopped = main.Position.Distance(_poseLastPlayerPos) < 0.05f;
                if (stopped && main.Position.Distance(_poseAnchor) > PoseReformDistance)
                {
                    Vec3 walked = Flat(main.Position - _poseAnchor);
                    _poseAnchor = main.Position;
                    _poseForward = walked;
                    List<Vec3> spots = PoseSpots(_poseAnchor, _poseForward);
                    for (int i = 0; i < _thugAgents.Count; i++)
                        _poseSpots[_thugAgents[i]] = i < spots.Count ? spots[i] : _thugAgents[i].Position;
                }
                _poseLastPlayerPos = main.Position;
            }

            float rotation = _poseForward.AsVec2.RotationInRadians;
            foreach (Agent thug in _thugAgents)
            {
                if (!_poseSpots.TryGetValue(thug, out Vec3 spot)) continue;
                try
                {
                    MakeDocile(thug);
                    var worldPos = new WorldPosition(Mission.Scene, spot);
                    AgentNavigator navigator = thug.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
                    if (navigator != null)
                        navigator.SetTargetFrame(worldPos, rotation, 0.2f, 0.1f, Agent.AIScriptedFrameFlags.NoAttack, true);
                    else
                        thug.SetScriptedPositionAndDirection(ref worldPos, rotation, false, Agent.AIScriptedFrameFlags.NoAttack);
                }
                catch (Exception) { /* a thug that won't hold still just spoils the shot */ }
            }
        }

        private static Vec3 Flat(Vec3 v)
        {
            v.z = 0f;
            return v.LengthSquared > 0.0001f ? v.NormalizedCopy() : Vec3.Forward;
        }
    }
}
