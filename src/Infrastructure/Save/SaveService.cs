using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChaosArcadeTower.Infrastructure.Save
{
    public class RankingEntry
    {
        [JsonPropertyName("playerName")] public string PlayerName { get; set; } = "Player";
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("floorReached")] public int FloorReached { get; set; }
        [JsonPropertyName("dateUtc")] public string DateUtc { get; set; } = "";
    }

    public class RankingData
    {
        [JsonPropertyName("entries")] public List<RankingEntry> Entries { get; set; } = new();
    }

    public class PlayerPrefs
    {
        [JsonPropertyName("playerName")] public string PlayerName { get; set; } = "Player";
        [JsonPropertyName("masterVolume")] public float MasterVolume { get; set; } = 1f;
        [JsonPropertyName("fullscreen")] public bool Fullscreen { get; set; } = false;
    }

    public interface ISaveService
    {
        RankingData LoadRanking();
        void SaveRanking(RankingData data);
        void AddRankingEntry(RankingEntry entry);
        void ResetRanking();
        PlayerPrefs LoadPrefs();
        void SavePrefs(PlayerPrefs prefs);
    }

    public class JsonSaveService : ISaveService
    {
        private const int MAX_ENTRIES = 100;
        private readonly string _savePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public JsonSaveService(string savePath)
        {
            _savePath = savePath;
            Directory.CreateDirectory(_savePath);
        }

        public RankingData LoadRanking()
        {
            string path = Path.Combine(_savePath, "ranking.json");
            if (!File.Exists(path)) return new RankingData();
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RankingData>(json, _jsonOptions) ?? new RankingData();
        }

        public void SaveRanking(RankingData data)
        {
            string path = Path.Combine(_savePath, "ranking.json");
            string tempPath = path + ".tmp";
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, true);
        }

        public void AddRankingEntry(RankingEntry entry)
        {
            var data = LoadRanking();
            entry.DateUtc = DateTime.UtcNow.ToString("o");
            data.Entries.Add(entry);
            data.Entries = data.Entries.OrderByDescending(e => e.Score).Take(MAX_ENTRIES).ToList();
            SaveRanking(data);
        }

        public void ResetRanking()
        {
            SaveRanking(new RankingData());
        }

        public PlayerPrefs LoadPrefs()
        {
            string path = Path.Combine(_savePath, "prefs.json");
            if (!File.Exists(path)) return new PlayerPrefs();
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PlayerPrefs>(json, _jsonOptions) ?? new PlayerPrefs();
        }

        public void SavePrefs(PlayerPrefs prefs)
        {
            string path = Path.Combine(_savePath, "prefs.json");
            string tempPath = path + ".tmp";
            string json = JsonSerializer.Serialize(prefs, _jsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, true);
        }
    }
}
