using System.Text.Json;

namespace ChaosArcadeTower.Infrastructure.Balance
{
    public static class BalanceLoader
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static BalanceData LoadFromJson(string json)
        {
            return JsonSerializer.Deserialize<BalanceData>(json, _options) ?? new BalanceData();
        }
    }
}
