using System.Collections.Generic;
using System.Text;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.Presentation.Shared
{
    public static class PieceInfoFormatter
    {
        public static string Format(PieceInstance piece, int slot, List<PerkInstance>? perks = null)
        {
            var sb = new StringBuilder();
            string slotLabel = slot >= 0 ? $" (Slot {slot + 1})" : " (Reserve)";
            sb.AppendLine($"[b]{piece.Definition.Type}[/b]{slotLabel}");
            sb.AppendLine($"ID: {piece.Id}");
            sb.AppendLine();
            sb.AppendLine($"HP: {piece.CurrentHp} / {piece.MaxHp}");
            sb.AppendLine($"ATK: {piece.Atk}");
            sb.AppendLine($"Cooldown: {piece.EffectiveCooldown:F2}s");
            sb.AppendLine($"Value: {piece.Value}");

            if (piece.BonusHp != 0 || piece.BonusAtk != 0 || piece.CooldownMultiplier < 0.999f)
            {
                sb.AppendLine();
                sb.AppendLine("[color=#44ccff]Perk bonuses:[/color]");
                if (piece.BonusHp != 0) sb.AppendLine($"  +{piece.BonusHp} HP");
                if (piece.BonusAtk != 0) sb.AppendLine($"  +{piece.BonusAtk} ATK");
                if (piece.CooldownMultiplier < 0.999f)
                    sb.AppendLine($"  CD mult: x{piece.CooldownMultiplier:F2}");
            }

            if (piece.Enchant.HasValue)
                sb.AppendLine($"\n[color=#ff88ff]Enchant: {piece.Enchant.Value}[/color]");

            if (perks != null && perks.Count > 0)
            {
                var relevant = FindRelevantPerks(piece, perks);
                if (relevant.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("[color=#aaaaaa]Active perks:[/color]");
                    foreach (var (name, stacks) in relevant)
                        sb.AppendLine($"  {name} x{stacks}");
                }
            }

            return sb.ToString();
        }

        private static List<(string name, int stacks)> FindRelevantPerks(
            PieceInstance piece, List<PerkInstance> perks)
        {
            var result = new List<(string, int)>();
            foreach (var p in perks)
            {
                var def = p.Definition;

                if (def.Target == PerkTarget.Piece && p.TargetPieceId == piece.Id)
                {
                    result.Add((def.Name, p.Stacks));
                    continue;
                }

                if (def.Type == PerkType.Enchant && p.TargetPieceId == piece.Id)
                {
                    result.Add((def.Name, p.Stacks));
                    continue;
                }

                if (def.Type == PerkType.PieceType)
                {
                    string typeName = def.GetStringParam("piece_type");
                    if (piece.Definition.Type.ToString().Equals(typeName, System.StringComparison.OrdinalIgnoreCase))
                        result.Add((def.Name, p.Stacks));
                    continue;
                }

                if (def.Type == PerkType.Slot)
                {
                    int slotIdx = def.GetIntParam("slot_index", -1);
                    if (slotIdx >= 0 && piece.Id != null)
                        result.Add((def.Name, p.Stacks));
                    continue;
                }

                if (def.Target == PerkTarget.Player && def.Type == PerkType.Global)
                {
                    result.Add((def.Name, p.Stacks));
                }
            }
            return result;
        }
    }
}
