using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Infrastructure.Balance;

namespace ChaosArcadeTower.Infrastructure.Content
{
    public class ContentService
    {
        private readonly Dictionary<PieceType, PieceDefinition> _pieces = new();
        private readonly List<PerkDefinition> _perks = new();
        private readonly Dictionary<string, PerkDefinition> _perksById = new();
        private BalanceData _balance = new();

        public BalanceData Balance => _balance;
        public IReadOnlyDictionary<PieceType, PieceDefinition> Pieces => _pieces;
        public IReadOnlyList<PerkDefinition> Perks => _perks;

        public void LoadBalance(string json)
        {
            _balance = BalanceLoader.LoadFromJson(json);
            BuildPieceDefinitions();
        }

        public void LoadPerks(string json)
        {
            _perks.Clear();
            _perksById.Clear();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            var root = doc.RootElement;
            if (!root.TryGetProperty("perks", out var perksArray)) return;

            foreach (var elem in perksArray.EnumerateArray())
            {
                var perk = ParsePerk(elem);
                _perks.Add(perk);
                _perksById[perk.Id] = perk;
            }
        }

        public PerkDefinition? GetPerk(string id) =>
            _perksById.TryGetValue(id, out var p) ? p : null;

        public List<PerkDefinition> GetPerksByRarity(Rarity rarity) =>
            _perks.Where(p => p.Rarity == rarity).ToList();

        public PieceDefinition GetPieceDefinition(PieceType type) =>
            _pieces.TryGetValue(type, out var def) ? def : _pieces[PieceType.Pawn];

        private void BuildPieceDefinitions()
        {
            _pieces.Clear();
            foreach (var kv in _balance.Pieces)
            {
                if (Enum.TryParse<PieceType>(kv.Key, true, out var type))
                {
                    _pieces[type] = new PieceDefinition(
                        type, kv.Value.Tier,
                        kv.Value.Hp, kv.Value.Atk,
                        kv.Value.Cooldown, kv.Value.Value);
                }
            }
        }

        private static PerkDefinition ParsePerk(JsonElement elem)
        {
            var perk = new PerkDefinition
            {
                Id = elem.GetProperty("id").GetString() ?? "",
                Name = elem.GetProperty("name").GetString() ?? "",
                Rarity = ParseEnum<Rarity>(elem, "rarity"),
                Type = ParseEnum<PerkType>(elem, "type"),
                Target = ParseEnum<PerkTarget>(elem, "target"),
                Stacking = ParseEnum<StackingMode>(elem, "stacking"),
                MaxStacks = GetIntOr(elem, "maxStacks", 1),
                PowerScore = GetIntOr(elem, "powerScore", 0),
                Description = GetStringNested(elem, "ui", "desc") ?? GetStringOr(elem, "description", "")
            };

            if (elem.TryGetProperty("params", out var paramsEl))
            {
                foreach (var prop in paramsEl.EnumerateObject())
                {
                    perk.Params[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Number => prop.Value.TryGetInt32(out int i) ? (object)i : prop.Value.GetDouble(),
                        JsonValueKind.String => prop.Value.GetString()!,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.GetRawText()
                    };
                }
            }

            return perk;
        }

        private static T ParseEnum<T>(JsonElement elem, string prop) where T : struct
        {
            if (elem.TryGetProperty(prop, out var val))
            {
                string? s = val.GetString();
                if (s != null)
                {
                    s = s.Replace("_", "");
                    if (Enum.TryParse<T>(s, true, out var result))
                        return result;
                }
            }
            return default;
        }

        private static int GetIntOr(JsonElement elem, string prop, int def)
        {
            if (elem.TryGetProperty(prop, out var val) && val.TryGetInt32(out int i)) return i;
            string camel = ToCamelCase(prop);
            if (elem.TryGetProperty(camel, out val) && val.TryGetInt32(out i)) return i;
            string snake = ToSnakeCase(prop);
            if (elem.TryGetProperty(snake, out val) && val.TryGetInt32(out i)) return i;
            return def;
        }

        private static string GetStringOr(JsonElement elem, string prop, string def)
        {
            if (elem.TryGetProperty(prop, out var val)) return val.GetString() ?? def;
            return def;
        }

        private static string? GetStringNested(JsonElement elem, string outer, string inner)
        {
            if (elem.TryGetProperty(outer, out var outerEl) && outerEl.TryGetProperty(inner, out var innerEl))
                return innerEl.GetString();
            return null;
        }

        private static string ToCamelCase(string s)
        {
            var parts = s.Split('_');
            if (parts.Length <= 1) return s;
            return parts[0] + string.Join("", parts.Skip(1).Select(p =>
                p.Length > 0 ? char.ToUpper(p[0]) + p[1..] : p));
        }

        private static string ToSnakeCase(string s)
        {
            return string.Concat(s.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
        }
    }
}
