using System;
using System.IO;
using System.Linq;
using ChaosArcadeTower.AI;
using ChaosArcadeTower.Infrastructure.Balance;
using ChaosArcadeTower.Infrastructure.Content;

namespace ChaosArcadeTower.Tests.AI
{
    public static class BotScalingTests
    {
        private const int BOT_COUNT = 100;
        private const int TARGET_FLOOR = 40;

        public static void RunAll()
        {
            var (sim, balance, powerService) = BuildServices();

            HighFloorBotsHaveMinPerks(sim, balance);
            HighFloorBotsHaveMinBoardPower(sim, balance, powerService);
            Determinism_SameSeedSameBot(sim);

            Console.WriteLine("[BotScalingTests] All tests passed.");
        }

        public static void HighFloorBotsHaveMinPerks(BotRunSimulator sim, BalanceData balance)
        {
            int minPerks = GetFloorBand(balance.BotGeneration.MinPerksByFloor, TARGET_FLOOR, 0);
            int failures = 0;

            for (int i = 0; i < BOT_COUNT; i++)
            {
                var bot = sim.Generate(1000 + i, TARGET_FLOOR, 0);
                if (bot.Perks.Count < minPerks)
                    failures++;
            }

            if (failures > 0)
                throw new Exception($"HighFloorBotsHaveMinPerks FAILED: {failures}/{BOT_COUNT} bots had fewer than {minPerks} perks at floor {TARGET_FLOOR}.");
        }

        public static void HighFloorBotsHaveMinBoardPower(BotRunSimulator sim, BalanceData balance,
            BoardPowerService powerService)
        {
            float minPower = GetFloorBandFloat(balance.BotGeneration.MinBoardPowerByFloor, TARGET_FLOOR, 0f);
            if (minPower <= 0) return;

            int failures = 0;
            float lowestPower = float.MaxValue;

            for (int i = 0; i < BOT_COUNT; i++)
            {
                var bot = sim.Generate(2000 + i, TARGET_FLOOR, 0);
                float power = powerService.Calculate(bot.Board, bot.Perks);
                if (power < lowestPower) lowestPower = power;
                if (power < minPower)
                    failures++;
            }

            if (failures > 0)
                throw new Exception($"HighFloorBotsHaveMinBoardPower FAILED: {failures}/{BOT_COUNT} bots below {minPower} power (lowest: {lowestPower:F1}) at floor {TARGET_FLOOR}.");
        }

        public static void Determinism_SameSeedSameBot(BotRunSimulator sim)
        {
            var bot1 = sim.Generate(42, TARGET_FLOOR, 0);
            var bot2 = sim.Generate(42, TARGET_FLOOR, 0);

            if (bot1.Perks.Count != bot2.Perks.Count)
                throw new Exception($"Determinism FAILED: perk counts differ ({bot1.Perks.Count} vs {bot2.Perks.Count}).");
            if (bot1.Wins != bot2.Wins)
                throw new Exception($"Determinism FAILED: wins differ ({bot1.Wins} vs {bot2.Wins}).");
            if (bot1.Score != bot2.Score)
                throw new Exception($"Determinism FAILED: scores differ ({bot1.Score} vs {bot2.Score}).");

            for (int i = 0; i < bot1.Perks.Count; i++)
            {
                if (bot1.Perks[i].Definition.Id != bot2.Perks[i].Definition.Id)
                    throw new Exception($"Determinism FAILED: perk {i} differs ({bot1.Perks[i].Definition.Id} vs {bot2.Perks[i].Definition.Id}).");
            }
        }

        private static (BotRunSimulator sim, BalanceData balance, BoardPowerService power) BuildServices()
        {
            string balancePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "Assets", "Game", "Data", "Configs", "Balance", "balance_v0_1.json");
            string perksPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "Assets", "Game", "Data", "Configs", "Perks", "perks_v0_1.json");

            if (!File.Exists(balancePath))
                balancePath = Path.Combine("Assets", "Game", "Data", "Configs", "Balance", "balance_v0_1.json");
            if (!File.Exists(perksPath))
                perksPath = Path.Combine("Assets", "Game", "Data", "Configs", "Perks", "perks_v0_1.json");

            string balanceJson = File.ReadAllText(balancePath);
            string perksJson = File.ReadAllText(perksPath);

            var content = new ContentService();
            content.LoadBalance(balanceJson);
            content.LoadPerks(perksJson);

            var balance = content.Balance;
            var dropTable = new DropTableService(balance.Drops);
            var rarityService = new RewardRarityService(balance.Rewards);
            var difficultyService = new DifficultyService(balance.Difficulty);
            var powerService = new BoardPowerService(balance.Globals.BoardPowerWeights);

            var sim = new BotRunSimulator(content, dropTable, rarityService, difficultyService, powerService, balance);
            return (sim, balance, powerService);
        }

        private static int GetFloorBand(System.Collections.Generic.Dictionary<string, int> bands, int floor, int def)
        {
            int best = def;
            int bestFloor = 0;
            foreach (var kv in bands)
            {
                if (int.TryParse(kv.Key, out int threshold) && floor >= threshold && threshold >= bestFloor)
                { best = kv.Value; bestFloor = threshold; }
            }
            return best;
        }

        private static float GetFloorBandFloat(System.Collections.Generic.Dictionary<string, float> bands, int floor, float def)
        {
            float best = def;
            int bestFloor = 0;
            foreach (var kv in bands)
            {
                if (int.TryParse(kv.Key, out int threshold) && floor >= threshold && threshold >= bestFloor)
                { best = kv.Value; bestFloor = threshold; }
            }
            return best;
        }
    }
}
