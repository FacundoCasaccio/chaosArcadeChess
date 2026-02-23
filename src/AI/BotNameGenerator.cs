using ChaosArcadeTower.Core.Random;

namespace ChaosArcadeTower.AI
{
    public static class BotNameGenerator
    {
        private static readonly string[] _prefixes =
        {
            "Shadow", "Iron", "Storm", "Dark", "Frost", "Blaze", "Stone", "Night",
            "Thunder", "Steel", "Crimson", "Golden", "Silver", "Void", "Obsidian",
            "Crystal", "Onyx", "Ember", "Rune", "Jade", "Scarlet", "Titan"
        };

        private static readonly string[] _suffixes =
        {
            "Knight", "Rook", "Pawn", "Bishop", "King", "Queen", "Guardian",
            "Slayer", "Warden", "Striker", "Master", "Sage", "Hunter",
            "Champion", "Breaker", "Walker", "Seeker", "Fury", "Blade"
        };

        public static string Generate(IRandomService rng)
        {
            string prefix = _prefixes[rng.NextInt(_prefixes.Length)];
            string suffix = _suffixes[rng.NextInt(_suffixes.Length)];
            int number = rng.NextInt(10, 99);
            return $"{prefix}{suffix}{number}";
        }
    }
}
