using System.Collections.Generic;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Simulation.Combat;

namespace ChaosArcadeTower.Simulation.Effects
{
    public class StatPerkEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            if (perk.TargetPieceId == null) return;

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece == null || piece.Id != perk.TargetPieceId) continue;
                ApplyStatBoosts(piece, perk);
                break;
            }
            foreach (var piece in board.Reserve)
            {
                if (piece.Id == perk.TargetPieceId)
                    ApplyStatBoosts(piece, perk);
            }
        }

        private void ApplyStatBoosts(PieceInstance piece, PerkInstance perk)
        {
            var def = perk.Definition;
            int stacks = perk.Stacks;
            int addHp = def.GetIntParam("add_hp") * stacks;
            int addAtk = def.GetIntParam("add_atk") * stacks;
            float cdMult = def.GetFloatParam("cooldown_mult", 1f);

            piece.BonusHp += addHp;
            piece.BonusAtk += addAtk;
            if (cdMult < 1f)
            {
                for (int s = 0; s < stacks; s++)
                    piece.CooldownMultiplier *= cdMult;
            }
            piece.ApplyBonuses();
        }
    }

    public class GlobalStatPerkEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            var def = perk.Definition;
            int stacks = perk.Stacks;
            int addHpAll = def.GetIntParam("add_hp_all") * stacks;
            int addAtkAll = def.GetIntParam("add_atk_all") * stacks;
            float cdMultAll = def.GetFloatParam("cooldown_mult_all", 1f);

            bool conditionMet = CheckConditions(def, board);
            if (!conditionMet) return;

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece == null) continue;
                piece.BonusHp += addHpAll;
                piece.BonusAtk += addAtkAll;
                if (cdMultAll < 1f)
                {
                    for (int s = 0; s < stacks; s++)
                        piece.CooldownMultiplier *= cdMultAll;
                }
                piece.ApplyBonuses();
            }
        }

        private bool CheckConditions(PerkDefinition def, BoardState board)
        {
            string reqPiece = def.GetStringParam("if_has_piece_type");
            if (!string.IsNullOrEmpty(reqPiece))
            {
                bool found = false;
                if (System.Enum.TryParse<PieceType>(reqPiece, true, out var pt))
                {
                    for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                    {
                        var p = board.GetSlot(i);
                        if (p != null && !p.IsDead && p.Definition.Type == pt)
                        { found = true; break; }
                    }
                }
                return found;
            }
            return true;
        }
    }

    public class SlotPerkEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            int slotIdx = perk.Definition.GetIntParam("slot_index", -1);
            if (slotIdx < 0 || slotIdx >= BoardState.ACTIVE_SLOTS) return;

            var piece = board.GetSlot(slotIdx);
            if (piece == null) return;

            int stacks = perk.Stacks;
            piece.BonusHp += perk.Definition.GetIntParam("add_hp") * stacks;
            piece.BonusAtk += perk.Definition.GetIntParam("add_atk") * stacks;
            piece.ApplyBonuses();
        }
    }

    public class PieceTypePerkEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            string typeName = perk.Definition.GetStringParam("piece_type");
            if (!System.Enum.TryParse<PieceType>(typeName, true, out var targetType)) return;

            int stacks = perk.Stacks;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece == null || piece.Definition.Type != targetType) continue;
                piece.BonusHp += perk.Definition.GetIntParam("add_hp") * stacks;
                piece.BonusAtk += perk.Definition.GetIntParam("add_atk") * stacks;
                float cdMult = perk.Definition.GetFloatParam("cooldown_mult", 1f);
                if (cdMult < 1f)
                    for (int s = 0; s < stacks; s++)
                        piece.CooldownMultiplier *= cdMult;
                piece.ApplyBonuses();
            }
        }
    }

    public class EnchantPerkEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            string enchantName = perk.Definition.GetStringParam("enchant");
            if (!System.Enum.TryParse<Enchantment>(enchantName, true, out var enchant)) return;

            if (perk.TargetPieceId != null)
            {
                for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                {
                    var piece = board.GetSlot(i);
                    if (piece?.Id == perk.TargetPieceId)
                    {
                        piece.Enchant = enchant;
                        break;
                    }
                }
            }
        }
    }
}
