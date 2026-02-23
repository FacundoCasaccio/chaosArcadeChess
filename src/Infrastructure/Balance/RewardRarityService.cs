using System;
using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Perks;

namespace ChaosArcadeTower.Infrastructure.Balance
{
    public class RewardRarityService
    {
        private readonly RewardsData _rewards;

        public RewardRarityService(RewardsData rewards)
        {
            _rewards = rewards;
        }

        public int GetChoiceCount(bool won)
        {
            return won ? _rewards.ChoicesOnWin : _rewards.ChoicesOnLoss;
        }

        public Rarity RollRarity(int floor, bool won, IRandomService rng)
        {
            var weights = GetRarityWeights(floor);
            if (!won)
                weights = ApplyLossShift(weights);
            return WeightedRarityPick(weights, rng);
        }

        private Dictionary<string, float> GetRarityWeights(int floor)
        {
            var band = DropTableService.GetFloorBand(floor, _rewards.RarityWeightsByFloorBand);
            return band ?? new Dictionary<string, float>
            {
                { "common", 0.85f }, { "rare", 0.15f }, { "epic", 0f }, { "unique", 0f }
            };
        }

        private Dictionary<string, float> ApplyLossShift(Dictionary<string, float> weights)
        {
            float shift = _rewards.LossPenaltyRarityShift.ShiftFraction;
            var result = new Dictionary<string, float>(weights);

            float uniqueShift = result.GetValueOrDefault("unique") * shift;
            float epicShift = result.GetValueOrDefault("epic") * shift;
            float rareShift = result.GetValueOrDefault("rare") * shift;

            result["unique"] = result.GetValueOrDefault("unique") - uniqueShift;
            result["epic"] = result.GetValueOrDefault("epic") - epicShift + uniqueShift;
            result["rare"] = result.GetValueOrDefault("rare") - rareShift + epicShift;
            result["common"] = result.GetValueOrDefault("common") + rareShift;

            return result;
        }

        private static Rarity WeightedRarityPick(Dictionary<string, float> weights, IRandomService rng)
        {
            float total = weights.Values.Sum();
            float roll = rng.NextFloat() * total;
            float cumulative = 0;

            foreach (var kv in weights.OrderBy(w => w.Key))
            {
                cumulative += kv.Value;
                if (roll <= cumulative)
                {
                    return kv.Key.ToLower() switch
                    {
                        "common" => Rarity.Common,
                        "rare" => Rarity.Rare,
                        "epic" => Rarity.Epic,
                        "unique" => Rarity.Unique,
                        _ => Rarity.Common
                    };
                }
            }
            return Rarity.Common;
        }
    }
}
