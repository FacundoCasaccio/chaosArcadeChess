using System;
using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Infrastructure.Balance;
using ChaosArcadeTower.Infrastructure.Content;

namespace ChaosArcadeTower.AI
{
    public class BotLoadout
    {
        public BoardState Board { get; set; } = new();
        public List<PerkInstance> Perks { get; set; } = new();
        public string BotName { get; set; } = "Bot";
        public int Wins { get; set; }
        public int Score { get; set; }
        public int Lives { get; set; } = 3;
    }

    public class BotRunSimulator
    {
        private readonly ContentService _content;
        private readonly DropTableService _dropTable;
        private readonly RewardRarityService _rarityService;
        private readonly DifficultyService _difficultyService;
        private readonly BoardPowerService _powerService;
        private readonly BalanceData _balance;

        public BotRunSimulator(
            ContentService content,
            DropTableService dropTable,
            RewardRarityService rarityService,
            DifficultyService difficultyService,
            BoardPowerService powerService,
            BalanceData balance)
        {
            _content = content;
            _dropTable = dropTable;
            _rarityService = rarityService;
            _difficultyService = difficultyService;
            _powerService = powerService;
            _balance = balance;
        }

        public BotLoadout Generate(int globalSeed, int floorIndex, int botId = 0)
        {
            int seed = SeededRandomService.CombineSeed(globalSeed, floorIndex, botId);
            var rng = new SeededRandomService(seed);

            for (int attempt = 0; attempt <= _balance.BotGeneration.MaxRestarts; attempt++)
            {
                var attemptRng = attempt == 0 ? rng : new SeededRandomService(
                    SeededRandomService.CombineSeed(seed, floorIndex, attempt));

                var result = TryGenerate(attemptRng, floorIndex);
                if (result != null && MeetsFloorMinimums(result, floorIndex))
                    return result;
            }

            return CreateFallbackBot(new SeededRandomService(seed + 9999), floorIndex);
        }

        private bool MeetsFloorMinimums(BotLoadout bot, int floorIndex)
        {
            int minPerks = GetFloorBandValue(_balance.BotGeneration.MinPerksByFloor, floorIndex, 0);
            if (bot.Perks.Count < minPerks) return false;

            float minPower = GetFloorBandValue(_balance.BotGeneration.MinBoardPowerByFloor, floorIndex, 0f);
            if (minPower > 0)
            {
                float power = _powerService.Calculate(bot.Board, bot.Perks);
                if (power < minPower) return false;
            }
            return true;
        }

        private static T GetFloorBandValue<T>(Dictionary<string, T> bands, int floor, T defaultVal)
        {
            T best = defaultVal;
            int bestFloor = 0;
            foreach (var kv in bands)
            {
                if (int.TryParse(kv.Key, out int threshold) && floor >= threshold && threshold >= bestFloor)
                {
                    best = kv.Value;
                    bestFloor = threshold;
                }
            }
            return best;
        }

        private BotLoadout? TryGenerate(IRandomService rng, int floorIndex)
        {
            var archetype = PickArchetype(rng);
            var board = GenerateInitialBoard(rng, 1);
            var perks = new List<PerkInstance>();
            int wins = 0;
            int score = 0;
            int lives = _balance.Globals.MaxLives;

            for (int floor = 1; floor < floorIndex; floor++)
            {
                float boardPower = _powerService.Calculate(board, perks);
                float expectedPower = _difficultyService.GetExpectedPower(floor);
                bool won = SimulateWinLoss(boardPower, expectedPower, rng);

                if (won) wins++;
                else lives--;

                if (lives <= 0) return null;

                bool applyReward = won || _balance.BotGeneration.ApplyRewardsOnLoss;
                if (applyReward)
                {
                    var rarity = _rarityService.RollRarity(floor, won, rng);
                    ApplyReward(board, perks, rarity, archetype, rng, floor);
                }

                if (rng.NextFloat() < 0.15f)
                    TrySwapPiece(board, rng, floor);

                if (won)
                    score += rng.NextInt(20, 60);
            }

            ApplyClampRules(perks);

            return new BotLoadout
            {
                Board = board,
                Perks = perks,
                BotName = BotNameGenerator.Generate(rng),
                Wins = wins,
                Score = score,
                Lives = lives
            };
        }

        private BoardState GenerateInitialBoard(IRandomService rng, int floor)
        {
            var board = new BoardState();
            var types = _dropTable.RollPieces(_balance.BotGeneration.InitialBoardSize, floor, rng);

            for (int i = 0; i < types.Count && i < BoardState.ACTIVE_SLOTS; i++)
            {
                var def = _content.GetPieceDefinition(types[i]);
                board.SetSlot(i, new PieceInstance(def, $"bot_{i}"));
            }

            return board;
        }

        private BotArchetype PickArchetype(IRandomService rng)
        {
            var values = Enum.GetValues(typeof(BotArchetype));
            return (BotArchetype)values.GetValue(rng.NextInt(values.Length))!;
        }

        private bool SimulateWinLoss(float boardPower, float expectedPower, IRandomService rng)
        {
            if (expectedPower <= 0) return true;
            float ratio = boardPower / expectedPower;
            float noise = (float)(rng.NextDouble() * 2 - 1) *
                _balance.Difficulty.WinProbabilityNoise.Stddev;
            noise = Math.Clamp(noise,
                -_balance.Difficulty.WinProbabilityNoise.ClampAbs,
                _balance.Difficulty.WinProbabilityNoise.ClampAbs);
            float winChance = Math.Clamp(0.5f + (ratio - 1f) * 2f + noise, 0.15f, 0.85f);
            return rng.NextFloat() < winChance;
        }

        private void ApplyReward(BoardState board, List<PerkInstance> perks,
            Rarity rarity, BotArchetype archetype, IRandomService rng, int floor)
        {
            var candidates = _content.GetPerksByRarity(rarity);
            if (candidates.Count == 0)
                candidates = _content.GetPerksByRarity(Rarity.Common);
            if (candidates.Count == 0) return;

            var chosen = PickPerkForArchetype(candidates, archetype, rng, board, perks);

            string addPieceType = chosen.GetStringParam("add_piece_type");
            if (!string.IsNullOrEmpty(addPieceType))
            {
                HandleAddPieceReward(board, perks, addPieceType, rng);
                return;
            }

            string transformTo = chosen.GetStringParam("transform_to");
            if (!string.IsNullOrEmpty(transformTo))
            {
                string reqType = chosen.GetStringParam("require_piece_type");
                if (Enum.TryParse<PieceType>(transformTo, true, out var newPt) &&
                    Enum.TryParse<PieceType>(reqType, true, out var reqPt))
                {
                    for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                    {
                        var p = board.GetSlot(i);
                        if (p != null && !p.IsDead && p.Definition.Type == reqPt)
                        {
                            var newDef = _content.GetPieceDefinition(newPt);
                            var newPiece = new PieceInstance(newDef, p.Id)
                            {
                                BonusHp = p.BonusHp, BonusAtk = p.BonusAtk,
                                CooldownMultiplier = p.CooldownMultiplier, Enchant = p.Enchant
                            };
                            newPiece.ApplyBonuses();
                            board.SetSlot(i, newPiece);
                            break;
                        }
                    }
                }
                return;
            }

            var existing = perks.FirstOrDefault(p => p.Definition.Id == chosen.Id);

            if (existing != null && existing.CanStack)
            {
                existing.Stacks++;
            }
            else if (existing == null)
            {
                var instance = new PerkInstance(chosen);
                if (chosen.Target == PerkTarget.Piece)
                    AssignPerkTarget(instance, board, rng);
                perks.Add(instance);
            }
        }

        private void HandleAddPieceReward(BoardState board, List<PerkInstance> perks,
            string pieceTypeName, IRandomService rng)
        {
            if (!Enum.TryParse<PieceType>(pieceTypeName, true, out var pt)) return;
            var def = _content.GetPieceDefinition(pt);
            var newPiece = new PieceInstance(def, $"bot_r_{board.Reserve.Count + board.GetAllPieces().Count}");

            if (board.Reserve.Count < board.MaxReserve)
            {
                board.Reserve.Add(newPiece);
                return;
            }

            if (board.Reserve.Count == 0) return;

            float currentPower = _powerService.Calculate(board, perks);
            int weakestIdx = 0;
            float bestDelta = float.MinValue;
            for (int r = 0; r < board.Reserve.Count; r++)
            {
                var saved = board.Reserve[r];
                board.Reserve[r] = newPiece;
                float newPower = _powerService.Calculate(board, perks);
                board.Reserve[r] = saved;
                float delta = newPower - currentPower;
                if (delta > bestDelta)
                {
                    bestDelta = delta;
                    weakestIdx = r;
                }
            }

            if (bestDelta > 0)
                board.Reserve[weakestIdx] = newPiece;
        }

        private PerkDefinition PickPerkForArchetype(List<PerkDefinition> candidates,
            BotArchetype archetype, IRandomService rng, BoardState board, List<PerkInstance> perks)
        {
            if (candidates.Count <= 3)
                return candidates[rng.NextInt(candidates.Count)];

            var scored = candidates.Select(c => (perk: c,
                score: ScoreForArchetype(c, archetype, rng) + SynergyBias(c, board, perks))).ToList();
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            int pick = rng.NextInt(Math.Min(3, scored.Count));
            return scored[pick].perk;
        }

        private float SynergyBias(PerkDefinition perk, BoardState board, List<PerkInstance> perks)
        {
            var cfg = _balance.BotGeneration;
            float bonus = cfg.SynergyBiasBonus;
            int threshold = cfg.SynergyDistanceThreshold;

            string condType = perk.GetStringParam("cond_all_piece_type");
            if (!string.IsNullOrEmpty(condType) && Enum.TryParse<PieceType>(condType, true, out var allPt))
            {
                int count = CountPieceType(board, allPt);
                int alive = board.CountAlive();
                if (alive > 0 && alive - count <= threshold)
                    return bonus;
            }

            condType = perk.GetStringParam("cond_min_piece_type");
            if (!string.IsNullOrEmpty(condType) && Enum.TryParse<PieceType>(condType, true, out var minPt))
            {
                int needed = perk.GetIntParam("cond_min_count", 3);
                int have = CountPieceType(board, minPt);
                if (needed - have <= threshold && needed - have > 0)
                    return bonus;
                if (have >= needed)
                    return bonus * 0.5f;
            }

            condType = perk.GetStringParam("cond_piece_type_extremes");
            if (!string.IsNullOrEmpty(condType) && Enum.TryParse<PieceType>(condType, true, out var exPt))
            {
                var first = board.GetSlot(0);
                var last = board.GetSlot(BoardState.ACTIVE_SLOTS - 1);
                int have = (first != null && first.Definition.Type == exPt ? 1 : 0)
                         + (last != null && last.Definition.Type == exPt ? 1 : 0);
                if (2 - have <= threshold)
                    return bonus;
            }

            if (perk.GetStringParam("cond_has_queen_and_king") == "True")
            {
                bool hasQ = false, hasK = false;
                for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                {
                    var p = board.GetSlot(i);
                    if (p == null) continue;
                    if (p.Definition.Type == PieceType.Queen) hasQ = true;
                    if (p.Definition.Type == PieceType.King) hasK = true;
                }
                int dist = (hasQ ? 0 : 1) + (hasK ? 0 : 1);
                if (dist <= threshold) return bonus;
            }

            foreach (var held in perks)
            {
                string heldCond = held.Definition.GetStringParam("cond_min_piece_type");
                if (string.IsNullOrEmpty(heldCond)) continue;
                string addPt = perk.GetStringParam("add_piece_type");
                if (!string.IsNullOrEmpty(addPt) && addPt.Equals(heldCond, StringComparison.OrdinalIgnoreCase))
                    return bonus * 0.7f;
            }

            return 0f;
        }

        private static int CountPieceType(BoardState board, PieceType type)
        {
            int count = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p != null && !p.IsDead && p.Definition.Type == type) count++;
            }
            foreach (var r in board.Reserve)
                if (r.Definition.Type == type) count++;
            return count;
        }

        private float ScoreForArchetype(PerkDefinition perk, BotArchetype archetype, IRandomService rng)
        {
            float score = perk.PowerScore + rng.NextFloat(-2f, 2f);
            switch (archetype)
            {
                case BotArchetype.Aggro:
                    if (perk.Params.ContainsKey("add_atk") || perk.Params.ContainsKey("add_atk_all"))
                        score += 5;
                    break;
                case BotArchetype.Sustain:
                    if (perk.Params.ContainsKey("add_hp") || perk.Params.ContainsKey("add_hp_all"))
                        score += 5;
                    break;
                case BotArchetype.Synergy:
                    if (perk.Type == PerkType.PieceType || perk.Type == PerkType.Enchant)
                        score += 5;
                    break;
                case BotArchetype.HighRoll:
                    score += perk.PowerScore * 0.3f;
                    break;
            }
            return score;
        }

        private void AssignPerkTarget(PerkInstance perk, BoardState board, IRandomService rng)
        {
            var alive = board.GetAlivePieces();
            if (alive.Count > 0)
                perk.TargetPieceId = alive[rng.NextInt(alive.Count)].Id;
        }

        private void TrySwapPiece(BoardState board, IRandomService rng, int floor)
        {
            int slot = rng.NextInt(BoardState.ACTIVE_SLOTS);
            var newType = _dropTable.RollPiece(floor, rng);
            var def = _content.GetPieceDefinition(newType);
            var old = board.GetSlot(slot);
            if (old != null && def.Tier <= old.Definition.Tier) return;
            board.SetSlot(slot, new PieceInstance(def, $"bot_{slot}"));
        }

        private void ApplyClampRules(List<PerkInstance> perks)
        {
            var rules = _balance.BotGeneration.ClampRules;
            int epicCount = perks.Count(p => p.Definition.Rarity == Rarity.Epic);
            int uniqueCount = perks.Count(p => p.Definition.Rarity == Rarity.Unique);

            while (uniqueCount > rules.MaxUniquePerks)
            {
                var toRemove = perks.Last(p => p.Definition.Rarity == Rarity.Unique);
                perks.Remove(toRemove);
                uniqueCount--;
            }
            while (epicCount > rules.MaxEpicPerks)
            {
                var toRemove = perks.Last(p => p.Definition.Rarity == Rarity.Epic);
                perks.Remove(toRemove);
                epicCount--;
            }
            while (perks.Count > rules.MaxTotalPerks)
            {
                perks.RemoveAt(perks.Count - 1);
            }
        }

        private BotLoadout CreateFallbackBot(IRandomService rng, int floorIndex)
        {
            var board = GenerateInitialBoard(rng, floorIndex);
            var perks = new List<PerkInstance>();
            int minPerks = GetFloorBandValue(_balance.BotGeneration.MinPerksByFloor, floorIndex, 0);
            int perksSoFar = 0;
            for (int attempt = 0; attempt < minPerks * 3 && perksSoFar < minPerks; attempt++)
            {
                var rarity = _rarityService.RollRarity(floorIndex, true, rng);
                var candidates = _content.GetPerksByRarity(rarity);
                if (candidates.Count == 0) candidates = _content.GetPerksByRarity(Rarity.Common);
                if (candidates.Count == 0) break;
                var chosen = candidates[rng.NextInt(candidates.Count)];
                if (!string.IsNullOrEmpty(chosen.GetStringParam("add_piece_type"))) continue;
                if (!string.IsNullOrEmpty(chosen.GetStringParam("transform_to"))) continue;
                var existing = perks.FirstOrDefault(p => p.Definition.Id == chosen.Id);
                if (existing != null && existing.CanStack) { existing.Stacks++; perksSoFar++; }
                else if (existing == null) { perks.Add(new PerkInstance(chosen)); perksSoFar++; }
            }
            ApplyClampRules(perks);
            return new BotLoadout
            {
                Board = board,
                Perks = perks,
                BotName = BotNameGenerator.Generate(rng),
                Wins = floorIndex / 2,
                Score = floorIndex * 30,
                Lives = 1
            };
        }
    }
}
