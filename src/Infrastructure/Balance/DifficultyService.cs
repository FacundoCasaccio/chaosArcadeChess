using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosArcadeTower.Infrastructure.Balance
{
    public class DifficultyService
    {
        private readonly DifficultyData _data;

        public DifficultyService(DifficultyData data)
        {
            _data = data;
        }

        public float GetFloorScalar(int floor)
        {
            float scalar = _data.FloorScalar.Base + (floor - 1) * _data.FloorScalar.PerFloor;
            return Math.Min(scalar, _data.FloorScalar.Cap);
        }

        public float GetExpectedPower(int floor)
        {
            var curve = _data.ExpectedPowerCurve;
            if (curve == null || curve.Count == 0) return 50f;

            var points = curve
                .Select(kv => (floor: ParseFloorKey(kv.Key), power: kv.Value))
                .OrderBy(p => p.floor)
                .ToList();

            if (floor <= points[0].floor) return points[0].power;
            if (floor >= points[^1].floor) return points[^1].power;

            for (int i = 0; i < points.Count - 1; i++)
            {
                if (floor >= points[i].floor && floor <= points[i + 1].floor)
                {
                    float t = (float)(floor - points[i].floor) / (points[i + 1].floor - points[i].floor);
                    return points[i].power + t * (points[i + 1].power - points[i].power);
                }
            }

            return points[^1].power;
        }

        private static int ParseFloorKey(string key)
        {
            string num = new string(key.Where(char.IsDigit).ToArray());
            return int.TryParse(num, out int f) ? f : 1;
        }
    }
}
