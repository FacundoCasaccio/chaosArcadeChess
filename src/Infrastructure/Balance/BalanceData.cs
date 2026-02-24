using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ChaosArcadeTower.Infrastructure.Balance
{
    public class BalanceData
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "0.1";
        [JsonPropertyName("globals")] public GlobalsData Globals { get; set; } = new();
        [JsonPropertyName("pieces")] public Dictionary<string, PieceData> Pieces { get; set; } = new();
        [JsonPropertyName("difficulty")] public DifficultyData Difficulty { get; set; } = new();
        [JsonPropertyName("rewards")] public RewardsData Rewards { get; set; } = new();
        [JsonPropertyName("drops")] public DropsData Drops { get; set; } = new();
        [JsonPropertyName("botGeneration")] public BotGenerationData BotGeneration { get; set; } = new();
    }

    public class GlobalsData
    {
        [JsonPropertyName("combatDurationSeconds")] public float CombatDurationSeconds { get; set; } = 30;
        [JsonPropertyName("tickSeconds")] public float TickSeconds { get; set; } = 0.10f;
        [JsonPropertyName("maxLives")] public int MaxLives { get; set; } = 3;
        [JsonPropertyName("score")] public ScoreConfig Score { get; set; } = new();
        [JsonPropertyName("boardPowerWeights")] public BoardPowerWeights BoardPowerWeights { get; set; } = new();
    }

    public class ScoreConfig
    {
        [JsonPropertyName("emptySlotAttackPoints")] public int EmptySlotAttackPoints { get; set; } = 1;
        [JsonPropertyName("alliedPieceAliveAddsValue")] public bool AlliedPieceAliveAddsValue { get; set; } = true;
        [JsonPropertyName("enemyPieceKilledAddsValue")] public bool EnemyPieceKilledAddsValue { get; set; } = true;
    }

    public class BoardPowerWeights
    {
        [JsonPropertyName("hpWeight")] public float HpWeight { get; set; } = 1f;
        [JsonPropertyName("atkWeight")] public float AtkWeight { get; set; } = 4f;
        [JsonPropertyName("cooldownWeight")] public float CooldownWeight { get; set; } = 6f;
        [JsonPropertyName("valueWeight")] public float ValueWeight { get; set; }
        [JsonPropertyName("perkPowerMultiplier")] public float PerkPowerMultiplier { get; set; } = 1f;
    }

    public class PieceData
    {
        [JsonPropertyName("tier")] public int Tier { get; set; }
        [JsonPropertyName("hp")] public int Hp { get; set; }
        [JsonPropertyName("atk")] public int Atk { get; set; }
        [JsonPropertyName("cooldown")] public float Cooldown { get; set; }
        [JsonPropertyName("value")] public int Value { get; set; }
    }

    public class DifficultyData
    {
        [JsonPropertyName("floorScalar")] public FloorScalarData FloorScalar { get; set; } = new();
        [JsonPropertyName("winProbabilityNoise")] public WinNoiseData WinProbabilityNoise { get; set; } = new();
        [JsonPropertyName("expectedPowerCurve")] public Dictionary<string, float> ExpectedPowerCurve { get; set; } = new();
    }

    public class FloorScalarData
    {
        [JsonPropertyName("base")] public float Base { get; set; } = 1f;
        [JsonPropertyName("perFloor")] public float PerFloor { get; set; } = 0.045f;
        [JsonPropertyName("cap")] public float Cap { get; set; } = 2.75f;
    }

    public class WinNoiseData
    {
        [JsonPropertyName("stddev")] public float Stddev { get; set; } = 0.06f;
        [JsonPropertyName("clampAbs")] public float ClampAbs { get; set; } = 0.15f;
    }

    public class RewardsData
    {
        [JsonPropertyName("choicesOnWin")] public int ChoicesOnWin { get; set; } = 3;
        [JsonPropertyName("choicesOnLoss")] public int ChoicesOnLoss { get; set; } = 2;
        [JsonPropertyName("rewardTypeWeights")] public Dictionary<string, float> RewardTypeWeights { get; set; } = new();
        [JsonPropertyName("rarityWeightsByFloorBand")] public Dictionary<string, Dictionary<string, float>> RarityWeightsByFloorBand { get; set; } = new();
        [JsonPropertyName("lossPenaltyRarityShift")] public LossPenaltyData LossPenaltyRarityShift { get; set; } = new();
    }

    public class LossPenaltyData
    {
        [JsonPropertyName("shiftFraction")] public float ShiftFraction { get; set; } = 0.35f;
    }

    public class DropsData
    {
        [JsonPropertyName("piecePoolByFloorBand")] public Dictionary<string, Dictionary<string, float>> PiecePoolByFloorBand { get; set; } = new();
    }

    public class BotGenerationData
    {
        [JsonPropertyName("initialBoardSize")] public int InitialBoardSize { get; set; } = 5;
        [JsonPropertyName("reserveSize")] public int ReserveSize { get; set; } = 2;
        [JsonPropertyName("simulateAllPreviousFloors")] public bool SimulateAllPreviousFloors { get; set; } = true;
        [JsonPropertyName("maxRestarts")] public int MaxRestarts { get; set; } = 6;
        [JsonPropertyName("applyRewardsOnLoss")] public bool ApplyRewardsOnLoss { get; set; } = true;
        [JsonPropertyName("clampRules")] public ClampRulesData ClampRules { get; set; } = new();
        [JsonPropertyName("minPerksByFloor")] public Dictionary<string, int> MinPerksByFloor { get; set; } = new();
        [JsonPropertyName("synergyBiasBonus")] public float SynergyBiasBonus { get; set; } = 12f;
        [JsonPropertyName("synergyDistanceThreshold")] public int SynergyDistanceThreshold { get; set; } = 2;
        [JsonPropertyName("minBoardPowerByFloor")] public Dictionary<string, float> MinBoardPowerByFloor { get; set; } = new();
    }

    public class ClampRulesData
    {
        [JsonPropertyName("maxEpicPerks")] public int MaxEpicPerks { get; set; } = 4;
        [JsonPropertyName("maxUniquePerks")] public int MaxUniquePerks { get; set; } = 2;
        [JsonPropertyName("maxTotalPerks")] public int MaxTotalPerks { get; set; } = 10;
    }
}
