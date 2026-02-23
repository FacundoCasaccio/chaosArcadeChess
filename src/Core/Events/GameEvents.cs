using ChaosArcadeTower.Domain.Combat;

namespace ChaosArcadeTower.Core.Events
{
    public struct SceneChangeRequestedEvent
    {
        public string ScenePath;
        public object? Params;
    }

    public struct RunStartedEvent
    {
        public int Seed;
    }

    public struct FloorAdvancedEvent
    {
        public int NewFloor;
    }

    public struct CombatFinishedEvent
    {
        public CombatResult Result;
    }

    public struct RewardChosenEvent
    {
        public string PerkId;
    }

    public struct GameOverEvent
    {
        public int TotalScore;
        public int FloorsReached;
    }

    public struct LifeLostEvent
    {
        public int RemainingLives;
    }
}
