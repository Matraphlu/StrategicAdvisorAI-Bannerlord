using System;
using System.Collections.Generic;
using System.IO;
using StrategicAdvisorAI.AI;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;

namespace StrategicAdvisorAI
{
    public class StrategicAdvisorMissionBehavior : MissionBehavior
    {
        private StrategyBrain _brain;
        private BattleSession _session;

        private bool _initialized;
        private bool _finalized;
        private bool _disabledMission;

        private float _startupDelay;
        private float _orderTick;
        private float _replanTick;
        private float _phaseTime;

        private const float StartupAnalyzeDelay = 1.25f;
        private const float ReorderInterval = 5.0f;
        private const float ReplanInterval = 12.0f;

        private string BrainPath => System.IO.Path.Combine(BasePath.Name, "Modules", "StrategicAdvisorAI", "ModuleData", "brain.json");

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (_disabledMission)
                return;

            if (!_initialized)
            {
                _startupDelay += dt;
                if (_startupDelay >= StartupAnalyzeDelay)
                    TryInitializePlan();
                return;
            }

            HandleHotkeys();

            if (_session == null)
                return;

            _phaseTime += dt;
            _orderTick += dt;
            _replanTick += dt;

            UpdateEnemyChargeState();

            if (_replanTick >= ReplanInterval || ShouldEmergencyReplan())
            {
                _replanTick = 0f;
                Replan();
            }

            if (_orderTick >= ReorderInterval)
            {
                _orderTick = 0f;
                ExecutePlan();
            }
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            FinalizeLearning();
        }

        private void TryInitializePlan()
        {
            if (Mission == null || Mission.PlayerTeam == null)
                return;

            AdvisorBattleKind battleKind = DetectBattleKind(Mission);
            if (battleKind == AdvisorBattleKind.Disabled)
            {
                _disabledMission = true;
                InformationManager.DisplayMessage(new InformationMessage("[Strategic Advisor] Disabled for camp/naval mission."));
                return;
            }

            Team player = Mission.PlayerTeam;
            Team enemy = FeatureExtractor.FindMainEnemy(Mission, player);

            if (enemy == null)
                return;

            int playerCount = FeatureExtractor.CountHumans(player);
            int enemyCount = FeatureExtractor.CountHumans(enemy);

            if (playerCount < 3 || enemyCount < 3)
                return;

            AdvisorSiegeRole siegeRole = DetectSiegeRole(player, battleKind);

            _brain = StrategyBrain.LoadOrCreate(BrainPath, FeatureExtractor.FeatureDim);

            bool enemyCharge = false;
            float[] features = FeatureExtractor.Extract(Mission, battleKind, siegeRole, enemyCharge, false, false);
            BrainDecision decision = _brain.ChoosePlan(features, SelectPlanPool(battleKind, siegeRole));
            List<string> reasons = ReasonBuilder.Build(features, decision.PlanId, battleKind, siegeRole);

            _session = new BattleSession
            {
                PlanId = decision.PlanId,
                Features = features,
                Confidence = decision.Confidence,
                PlayerStartCount = playerCount,
                EnemyStartCount = enemyCount,
                BattleKind = battleKind,
                SiegeRole = siegeRole,
                AutoCommandEnabled = true,
                FollowChoiceMade = true,
                EnemyChargeDetected = false,
                LastEnemyDistance = DistanceToEnemyCenter(),
                LastPlanChangeTime = 0f
            };

            InformationManager.DisplayMessage(new InformationMessage("[Strategic Advisor] Auto command ON"));
            InformationManager.DisplayMessage(new InformationMessage("[Strategic Advisor] Plan: " + NicePlanName(decision.PlanId)));
            foreach (string reason in reasons)
                InformationManager.DisplayMessage(new InformationMessage(" - " + reason));
            InformationManager.DisplayMessage(new InformationMessage("[Strategic Advisor] Press O to toggle auto command ON/OFF."));

            ExecutePlan();
            _initialized = true;
        }

        private void Replan()
        {
            if (_session == null || Mission == null || Mission.PlayerTeam == null)
                return;

            Team player = Mission.PlayerTeam;
            Team enemy = FeatureExtractor.FindMainEnemy(Mission, player);
            if (enemy == null)
                return;

            float[] features = FeatureExtractor.Extract(
    Mission,
    _session.BattleKind,
    _session.SiegeRole,
    _session.EnemyChargeDetected,
    _session.BreakthroughDetected,
    _session.ExposedFlankDetected);
            BrainDecision decision = _brain.ChoosePlan(features, SelectPlanPool(_session.BattleKind, _session.SiegeRole));

            if (decision.PlanId != _session.PlanId || ShouldEmergencyReplan())
            {
                _session.PlanId = decision.PlanId;
                _session.Features = features;
                _session.Confidence = decision.Confidence;
                _session.LastPlanChangeTime = _phaseTime;

                InformationManager.DisplayMessage(new InformationMessage("[Strategic Advisor] Replan: " + NicePlanName(decision.PlanId)));
            }
        }

        private bool ShouldEmergencyReplan()
        {
            if (_session == null)
                return false;

            if (_session.EnemyChargeDetected && _session.BattleKind == AdvisorBattleKind.Field)
                return true;

            Team player = Mission?.PlayerTeam;
            Team enemy = FeatureExtractor.FindMainEnemy(Mission, player);
            if (player == null || enemy == null)
                return false;

            float dist = FeatureExtractor.EstimateTeamCenter(player).Distance(FeatureExtractor.EstimateTeamCenter(enemy));
            return dist < 35f && _phaseTime - _session.LastPlanChangeTime > 8f;
        }

        private void UpdateEnemyChargeState()
        {
            if (_session == null || Mission == null || Mission.PlayerTeam == null)
                return;

            Team enemy = FeatureExtractor.FindMainEnemy(Mission, Mission.PlayerTeam);
            if (enemy == null)
                return;

            float currentDist = DistanceToEnemyCenter();
            float delta = _session.LastEnemyDistance - currentDist;

            _session.EnemyChargeDetected = delta > 8f && currentDist < 80f;
            _session.LastEnemyDistance = currentDist;
        }

        private float DistanceToEnemyCenter()
        {
            Team player = Mission?.PlayerTeam;
            Team enemy = FeatureExtractor.FindMainEnemy(Mission, player);
            if (player == null || enemy == null)
                return 999f;

            return FeatureExtractor.EstimateTeamCenter(player).Distance(FeatureExtractor.EstimateTeamCenter(enemy));
        }

        private void FinalizeLearning()
        {
            if (_finalized || _session == null || _brain == null || Mission == null)
                return;

            Team player = Mission.PlayerTeam;
            Team enemy = FeatureExtractor.FindMainEnemy(Mission, player);

            _session.PlayerAliveAtEnd = FeatureExtractor.CountHumans(player);
            _session.EnemyAliveAtEnd = FeatureExtractor.CountHumans(enemy);
            _session.PlayerWon = _session.EnemyAliveAtEnd <= _session.PlayerAliveAtEnd;

            if (_session.FollowChoiceMade && _session.AutoCommandEnabled)
            {
                float reward = RewardCalculator.Calculate(_session);
                _brain.Update(_session.PlanId, _session.Features, reward);
                _brain.Save(BrainPath);
                InformationManager.DisplayMessage(new InformationMessage("[Strategic Advisor] Learning saved. Reward = " + reward.ToString("0.00")));
            }

            _finalized = true;
        }

        private void HandleHotkeys()
        {
            if (_session == null)
                return;

            if (Input.IsKeyPressed(InputKey.O))
            {
                _session.AutoCommandEnabled = !_session.AutoCommandEnabled;
                _session.FollowChoiceMade = true;

                InformationManager.DisplayMessage(new InformationMessage(
                    "[Strategic Advisor] Auto command: " + (_session.AutoCommandEnabled ? "ON" : "OFF")));
            }
        }

        private void ExecutePlan()
        {
            if (_session == null || !_session.AutoCommandEnabled || Mission == null || Mission.PlayerTeam == null)
                return;

            Team player = Mission.PlayerTeam;
            Team enemy = FeatureExtractor.FindMainEnemy(Mission, player);
            if (enemy == null)
                return;

            Formation infantry = GetFormation(player, FormationClass.Infantry);
            Formation ranged = GetFormation(player, FormationClass.Ranged);
            Formation cavalry = GetFormation(player, FormationClass.Cavalry);
            Formation horseArchers = GetFormation(player, FormationClass.HorseArcher);

            Vec3 playerCenter = FeatureExtractor.EstimateTeamCenter(player);
            Vec3 enemyCenter = FeatureExtractor.EstimateTeamCenter(enemy);

            Vec2 dir = enemyCenter.AsVec2 - playerCenter.AsVec2;
            if (dir.LengthSquared <= 0.0001f)
                dir = new Vec2(0f, 1f);
            else
                dir = dir.Normalized();

            Vec2 right = dir.RightVec();

            if (_session.BattleKind == AdvisorBattleKind.Siege)
            {
                ExecuteSiegePlan(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                return;
            }

            if (_session.EnemyChargeDetected)
            {
                OrderEmergencyResponse(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                return;
            }

            switch (_session.PlanId)
            {
                case PlanCatalog.DefenseHill:
                    OrderDefendHill(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.DefenseCompact:
                    OrderCompactDefense(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.AggressivePush:
                    OrderAggressivePush(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.RangedAnchor:
                    OrderRangedAnchor(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.CavalryHarass:
                    OrderCavalryHarass(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.FlankLeft:
                    OrderFlank(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right, true);
                    break;
                case PlanCatalog.FlankRight:
                    OrderFlank(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right, false);
                    break;
                case PlanCatalog.RushArchers:
                    OrderRushArchers(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.SkirmishDelay:
                    OrderSkirmishDelay(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
                case PlanCatalog.AllInCharge:
                    OrderAllInCharge(infantry, ranged, cavalry, horseArchers);
                    break;
                default:
                    OrderCompactDefense(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                    break;
            }
        }

        private void ExecuteSiegePlan(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            bool attacker = _session.SiegeRole == AdvisorSiegeRole.Attacker;

            if (attacker)
            {
                switch (_session.PlanId)
                {
                    case PlanCatalog.SiegeAttackLadders:
                        OrderSiegeAttackLadders(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeAttackGate:
                        OrderSiegeAttackGate(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeAttackMissilePressure:
                        OrderSiegeAttackMissilePressure(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeAttackReservePush:
                        OrderSiegeAttackReservePush(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeAttackSplitPressure:
                        OrderSiegeAttackSplitPressure(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    default:
                        OrderSiegeAttackGate(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                }
            }
            else
            {
                switch (_session.PlanId)
                {
                    case PlanCatalog.SiegeDefendWalls:
                        OrderSiegeDefendWalls(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeDefendLadders:
                        OrderSiegeDefendLadders(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeDefendGate:
                        OrderSiegeDefendGate(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeDefendMissileAttrition:
                        OrderSiegeDefendMissileAttrition(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    case PlanCatalog.SiegeDefendReserveCounter:
                        OrderSiegeDefendReserveCounter(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                    default:
                        OrderSiegeDefendWalls(infantry, ranged, cavalry, horseArchers, playerCenter, enemyCenter, dir, right);
                        break;
                }
            }
        }

        private void OrderEmergencyResponse(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, playerCenter);
            FaceFormation(infantry, enemyCenter);
            SetShieldWall(infantry);

            MoveFormation(ranged, Offset(playerCenter, -dir * 10f));
            FaceFormation(ranged, enemyCenter);
            SetLoose(ranged);

            MoveFormation(cavalry, Offset(playerCenter, right * 12f));
            FaceFormation(cavalry, enemyCenter);

            MoveFormation(horseArchers, Offset(playerCenter, -right * 12f));
            FaceFormation(horseArchers, enemyCenter);

            ChargeIfClose(cavalry, playerCenter, enemyCenter, 40f);
        }

        private void OrderDefendHill(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            Vec3 basePos = playerCenter; basePos.z += 5f;
            MoveFormation(infantry, basePos); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(basePos, -dir * 4f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(basePos, right * 20f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(basePos, -right * 20f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
        }

        private void OrderCompactDefense(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, playerCenter); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, -dir * 6f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(playerCenter, right * 12f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(playerCenter, -right * 12f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
        }

        private void OrderAggressivePush(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 20f)); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 8f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(playerCenter, right * 18f + dir * 15f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(playerCenter, -right * 18f + dir * 12f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
            ChargeIfClose(infantry, playerCenter, enemyCenter, 55f);
            ChargeIfClose(cavalry, playerCenter, enemyCenter, 70f);
        }

        private void OrderRangedAnchor(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, playerCenter); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, -dir * 10f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(playerCenter, right * 24f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(playerCenter, -right * 24f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
        }

        private void OrderCavalryHarass(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, playerCenter); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(playerCenter, -dir * 8f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(enemyCenter, right * 28f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(enemyCenter, -right * 28f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
        }

        private void OrderFlank(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right, bool left)
        {
            Vec2 flank = left ? -right : right;
            MoveFormation(infantry, playerCenter); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(playerCenter, -dir * 8f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(enemyCenter, flank * 30f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(playerCenter, flank * 18f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
        }

        private void OrderRushArchers(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 18f)); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 5f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(enemyCenter, right * 20f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(enemyCenter, -right * 20f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
            ChargeIfClose(cavalry, playerCenter, enemyCenter, 60f);
        }

        private void OrderSkirmishDelay(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, -dir * 10f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, -dir * 18f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(playerCenter, right * 18f)); FaceFormation(cavalry, enemyCenter);
            MoveFormation(horseArchers, Offset(playerCenter, -right * 18f)); FaceFormation(horseArchers, enemyCenter); SetLoose(horseArchers);
        }

        private void OrderAllInCharge(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers)
        {
            Charge(infantry); Charge(ranged); Charge(cavalry); Charge(horseArchers);
        }

        private void OrderSiegeAttackLadders(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(enemyCenter, -dir * 35f - right * 10f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 15f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
        }

        private void OrderSiegeAttackGate(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(enemyCenter, -dir * 25f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 10f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            if (_phaseTime > 20f) Charge(infantry);
        }

        private void OrderSiegeAttackMissilePressure(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 8f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 18f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
        }

        private void OrderSiegeAttackReservePush(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(enemyCenter, -dir * 30f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 10f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            if (_phaseTime > 30f) Charge(infantry);
        }

        private void OrderSiegeAttackSplitPressure(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(enemyCenter, -dir * 35f + right * 12f)); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 12f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            MoveFormation(cavalry, Offset(enemyCenter, -dir * 35f - right * 12f)); FaceFormation(cavalry, enemyCenter);
        }

        private void OrderSiegeDefendWalls(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 10f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 18f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
        }

        private void OrderSiegeDefendLadders(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 12f - right * 10f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 20f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
        }

        private void OrderSiegeDefendGate(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 8f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 16f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            if (_phaseTime > 25f) ChargeIfClose(infantry, playerCenter, enemyCenter, 35f);
        }

        private void OrderSiegeDefendMissileAttrition(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 5f)); FaceFormation(infantry, enemyCenter); SetLine(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 18f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
        }

        private void OrderSiegeDefendReserveCounter(Formation infantry, Formation ranged, Formation cavalry, Formation horseArchers, Vec3 playerCenter, Vec3 enemyCenter, Vec2 dir, Vec2 right)
        {
            MoveFormation(infantry, Offset(playerCenter, dir * 6f)); FaceFormation(infantry, enemyCenter); SetShieldWall(infantry);
            MoveFormation(ranged, Offset(playerCenter, dir * 16f)); FaceFormation(ranged, enemyCenter); SetLoose(ranged);
            if (_phaseTime > 35f) Charge(cavalry);
        }

        private static AdvisorBattleKind DetectBattleKind(Mission mission)
        {
            if (mission == null)
                return AdvisorBattleKind.Disabled;

            string lower = (mission.SceneName ?? string.Empty).ToLowerInvariant();

            if (lower.Contains("camp") || lower.Contains("hideout"))
                return AdvisorBattleKind.Disabled;

            if (lower.Contains("naval") || lower.Contains("ship") || lower.Contains("boat") || lower.Contains("sea"))
                return AdvisorBattleKind.Disabled;

            if (lower.Contains("siege") || lower.Contains("castle") || lower.Contains("town") || lower.Contains("wall") || lower.Contains("gate"))
                return AdvisorBattleKind.Siege;

            return AdvisorBattleKind.Field;
        }

        private static AdvisorSiegeRole DetectSiegeRole(Team player, AdvisorBattleKind battleKind)
        {
            if (battleKind != AdvisorBattleKind.Siege || player == null)
                return AdvisorSiegeRole.None;

            return player.Side == BattleSideEnum.Attacker ? AdvisorSiegeRole.Attacker : AdvisorSiegeRole.Defender;
        }

        private static List<string> SelectPlanPool(AdvisorBattleKind battleKind, AdvisorSiegeRole siegeRole)
        {
            if (battleKind == AdvisorBattleKind.Field)
                return PlanCatalog.FieldBattlePlans;

            if (battleKind == AdvisorBattleKind.Siege)
                return siegeRole == AdvisorSiegeRole.Attacker ? PlanCatalog.SiegeAttackerPlans : PlanCatalog.SiegeDefenderPlans;

            return PlanCatalog.FieldBattlePlans;
        }

        private static Formation GetFormation(Team team, FormationClass formationClass)
        {
            if (team == null)
                return null;

            Formation f = team.GetFormation(formationClass);
            if (f == null || f.CountOfUnits <= 0)
                return null;

            return f;
        }

        private static Vec3 Offset(Vec3 pos, Vec2 offset)
        {
            return new Vec3(pos.x + offset.x, pos.y + offset.y, pos.z, -1f);
        }

        private static void MoveFormation(Formation formation, Vec3 pos)
        {
            if (formation == null || Mission.Current == null || Mission.Current.Scene == null)
                return;

            WorldPosition wp = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, pos, false);
            formation.SetMovementOrder(MovementOrder.MovementOrderMove(wp));
        }

        private static void FaceFormation(Formation formation, Vec3 target)
        {
            if (formation == null || formation.Team == null)
                return;

            Vec3 teamCenter = FeatureExtractor.EstimateTeamCenter(formation.Team);
            Vec2 dir = target.AsVec2 - teamCenter.AsVec2;

            if (dir.LengthSquared <= 0.0001f)
                return;

            formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(dir.Normalized()));
        }

        private static void Charge(Formation formation)
        {
            if (formation == null) return;
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }

        private static void ChargeIfClose(Formation formation, Vec3 from, Vec3 to, float dist)
        {
            if (formation == null) return;
            if (from.Distance(to) <= dist)
                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }

        private static void SetShieldWall(Formation formation)
        {
            if (formation == null) return;
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderShieldWall);
        }

        private static void SetLine(Formation formation)
        {
            if (formation == null) return;
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
        }

        private static void SetLoose(Formation formation)
        {
            if (formation == null) return;
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLoose);
        }

        private static string NicePlanName(string id)
        {
            switch (id)
            {
                case PlanCatalog.DefenseHill: return "Defense on High Ground";
                case PlanCatalog.DefenseCompact: return "Compact Defensive Formation";
                case PlanCatalog.AggressivePush: return "Aggressive Push";
                case PlanCatalog.RangedAnchor: return "Ranged Anchor";
                case PlanCatalog.CavalryHarass: return "Cavalry Harassment";
                case PlanCatalog.FlankLeft: return "Left Flank Pressure";
                case PlanCatalog.FlankRight: return "Right Flank Pressure";
                case PlanCatalog.RushArchers: return "Rush Enemy Archers";
                case PlanCatalog.SkirmishDelay: return "Skirmish and Delay";
                case PlanCatalog.AllInCharge: return "Decisive Full Charge";
                case PlanCatalog.SiegeAttackLadders: return "Concentrate on Ladder Assaults";
                case PlanCatalog.SiegeAttackGate: return "Pressure the Gate";
                case PlanCatalog.SiegeAttackMissilePressure: return "Missile Pressure Before Entry";
                case PlanCatalog.SiegeAttackReservePush: return "Hold a Reserve for the First Opening";
                case PlanCatalog.SiegeAttackSplitPressure: return "Split Pressure Across Multiple Sectors";
                case PlanCatalog.SiegeDefendWalls: return "Hold Walls and Defensive Depth";
                case PlanCatalog.SiegeDefendLadders: return "Focus Ladder Choke Points";
                case PlanCatalog.SiegeDefendGate: return "Lock Down the Gate Sector";
                case PlanCatalog.SiegeDefendMissileAttrition: return "Win Through Missile Attrition";
                case PlanCatalog.SiegeDefendReserveCounter: return "Keep a Counterattack Reserve";
                default: return id;
            }
        }
    }
}