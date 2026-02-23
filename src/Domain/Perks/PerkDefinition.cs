using System.Collections.Generic;

namespace ChaosArcadeTower.Domain.Perks
{
    public enum PerkType
    {
        Stat,
        Slot,
        PieceType,
        Global,
        Enchant,
        OneShot
    }

    public enum PerkTarget
    {
        Piece,
        Slot,
        PieceType,
        Player
    }

    public enum StackingMode
    {
        None,
        Additive
    }

    public sealed class PerkDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public Rarity Rarity { get; set; }
        public PerkType Type { get; set; }
        public PerkTarget Target { get; set; }
        public StackingMode Stacking { get; set; }
        public int MaxStacks { get; set; } = 1;
        public Dictionary<string, object> Params { get; set; } = new();
        public int PowerScore { get; set; }
        public string Description { get; set; } = "";

        public T GetParam<T>(string key, T defaultValue = default!)
        {
            if (Params.TryGetValue(key, out var val))
            {
                if (val is T typed) return typed;
                try { return (T)System.Convert.ChangeType(val, typeof(T)); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        public float GetFloatParam(string key, float defaultValue = 0f)
        {
            if (Params.TryGetValue(key, out var val))
            {
                if (val is float f) return f;
                if (val is double d) return (float)d;
                if (val is int i) return i;
                if (val is long l) return l;
                if (float.TryParse(val?.ToString(), out float parsed)) return parsed;
            }
            return defaultValue;
        }

        public int GetIntParam(string key, int defaultValue = 0)
        {
            if (Params.TryGetValue(key, out var val))
            {
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is float f) return (int)f;
                if (val is double d) return (int)d;
                if (int.TryParse(val?.ToString(), out int parsed)) return parsed;
            }
            return defaultValue;
        }

        public string GetStringParam(string key, string defaultValue = "")
        {
            if (Params.TryGetValue(key, out var val))
                return val?.ToString() ?? defaultValue;
            return defaultValue;
        }

        public bool NeedsTargetSelection =>
            Target == PerkTarget.Piece || Type == PerkType.Enchant;
    }
}
