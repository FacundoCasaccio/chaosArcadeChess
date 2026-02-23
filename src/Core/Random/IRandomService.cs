namespace ChaosArcadeTower.Core.Random
{
    public interface IRandomService
    {
        int Seed { get; }
        int NextInt(int maxExclusive);
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        float NextFloat(float min, float max);
        double NextDouble();
        IRandomService Fork(int salt);
    }
}
