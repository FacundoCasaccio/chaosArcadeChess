using System;
using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.Infrastructure.Balance
{
    public class DropTableService
    {
        private readonly DropsData _drops;

        public DropTableService(DropsData drops)
        {
            _drops = drops;
        }

        public PieceType RollPiece(int floor, IRandomService rng)
        {
            var band = GetFloorBand(floor, _drops.PiecePoolByFloorBand);
            if (band == null) return PieceType.Pawn;
            return WeightedPick(band, rng);
        }

        public List<PieceType> RollPieces(int count, int floor, IRandomService rng)
        {
            var result = new List<PieceType>();
            for (int i = 0; i < count; i++)
                result.Add(RollPiece(floor, rng));
            return result;
        }

        private static PieceType WeightedPick(Dictionary<string, float> weights, IRandomService rng)
        {
            float total = weights.Values.Sum();
            float roll = rng.NextFloat() * total;
            float cumulative = 0;
            foreach (var kv in weights)
            {
                cumulative += kv.Value;
                if (roll <= cumulative)
                {
                    if (Enum.TryParse<PieceType>(kv.Key, true, out var pt))
                        return pt;
                }
            }
            return PieceType.Pawn;
        }

        public static Dictionary<string, float>? GetFloorBand(int floor, Dictionary<string, Dictionary<string, float>> bands)
        {
            foreach (var kv in bands)
            {
                if (MatchesBand(kv.Key, floor))
                    return kv.Value;
            }
            return bands.Values.LastOrDefault();
        }

        private static bool MatchesBand(string bandKey, int floor)
        {
            string key = bandKey.Replace("band_", "").Replace("band", "");
            if (key.EndsWith("plus") || key.EndsWith("_plus"))
            {
                string num = new string(key.TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(num, out int min) && floor >= min;
            }

            var parts = key.Split('_', '-').Where(s => int.TryParse(s, out _)).ToArray();
            if (parts.Length >= 2)
            {
                int min = int.Parse(parts[0]);
                int max = int.Parse(parts[1]);
                return floor >= min && floor <= max;
            }

            return false;
        }
    }
}
