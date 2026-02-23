namespace ChaosArcadeTower.Core.Random
{
    public class SeededRandomService : IRandomService
    {
        private readonly System.Random _rng;

        public int Seed { get; }

        public SeededRandomService(int seed)
        {
            Seed = seed;
            _rng = new System.Random(seed);
        }

        public int NextInt(int maxExclusive) => _rng.Next(maxExclusive);

        public int NextInt(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        public float NextFloat() => (float)_rng.NextDouble();

        public float NextFloat(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public double NextDouble() => _rng.NextDouble();

        public IRandomService Fork(int salt)
        {
            int combined = unchecked(Seed * 31 + salt);
            return new SeededRandomService(combined);
        }

        public static int CombineSeed(int baseSeed, int floorIndex, int botId)
        {
            unchecked
            {
                int h = baseSeed;
                h = h * 397 ^ floorIndex;
                h = h * 397 ^ botId;
                return h;
            }
        }
    }
}
