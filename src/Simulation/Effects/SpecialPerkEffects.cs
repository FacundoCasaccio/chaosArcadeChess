using System;
using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Simulation.Combat;

namespace ChaosArcadeTower.Simulation.Effects
{
    public class OneShotPerkEffect : IPerkEffect
    {
        private readonly HashSet<string> _triggered = new();

        public void OnTick(CombatContext ctx, PerkInstance perk, BoardState board, Side side,
            float elapsed, List<CombatEvent> events, IRandomService rng)
        {
            if (perk.ChargesRemaining <= 0) return;
            string key = $"{perk.Definition.Id}_{side}";
            if (_triggered.Contains(key)) return;

            float triggerAt = perk.Definition.GetFloatParam("trigger_at_seconds");
            if (elapsed < triggerAt) return;

            _triggered.Add(key);
            perk.ChargesRemaining--;

            float healPct = perk.Definition.GetFloatParam("heal_all_pct");
            if (healPct > 0)
            {
                for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                {
                    var piece = board.GetSlot(i);
                    if (piece == null || piece.IsDead) continue;
                    int healAmt = (int)(piece.MaxHp * healPct);
                    piece.Heal(healAmt);
                    events.Add(CombatEvent.Heal(elapsed, side, i, side, i, healAmt));
                }
                events.Add(new CombatEvent
                {
                    Timestamp = elapsed, Type = CombatEventType.PerkTriggered,
                    SourceSide = side, PerkId = perk.Definition.Id,
                    Description = $"{perk.Definition.Name}: healed all allies!"
                });
            }
        }
    }

    public class EmergencyPatchEffect : IPerkEffect
    {
        private readonly HashSet<string> _triggered = new();

        public void OnTick(CombatContext ctx, PerkInstance perk, BoardState board, Side side,
            float elapsed, List<CombatEvent> events, IRandomService rng)
        {
            string key = $"{perk.Definition.Id}_{side}";
            if (_triggered.Contains(key)) return;

            float triggerAt = perk.Definition.GetFloatParam("trigger_at_seconds", 7f);
            if (elapsed < triggerAt) return;
            _triggered.Add(key);

            int healAmt = perk.Definition.GetIntParam("heal_lowest", 6);
            PieceInstance? lowest = null;
            int lowestSlot = -1;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null || p.IsDead) continue;
                if (lowest == null || p.CurrentHp < lowest.CurrentHp)
                { lowest = p; lowestSlot = i; }
            }
            if (lowest == null) return;

            int hpBefore = lowest.CurrentHp;
            lowest.Heal(healAmt);
            events.Add(new CombatEvent
            {
                Timestamp = elapsed, Type = CombatEventType.PerkTriggered,
                SourceSide = side, PerkId = perk.Definition.Id,
                Description = $"ONE_SHOT Emergency Patch: healed {lowest.Definition.Type}({lowest.Id}) +{healAmt} ({hpBefore}->{lowest.CurrentHp})"
            });
        }
    }

    public class ArcBatteryEffect : IPerkEffect
    {
        private readonly HashSet<string> _triggered = new();

        public void OnDamageDealt(CombatContext ctx, PerkInstance perk, PieceInstance source,
            PieceInstance target, int damage, float timestamp, List<CombatEvent> events)
        {
            Side attackerSide = ctx.PlayerBoard.ContainsPiece(source) ? Side.Player : Side.Enemy;
            string key = $"{perk.Definition.Id}_{attackerSide}";
            if (_triggered.Contains(key)) return;
            _triggered.Add(key);

            int chainTargets = perk.Definition.GetIntParam("chain_targets", 1);
            float chainPct = perk.Definition.GetFloatParam("chain_damage_pct", 0.50f);
            int chainDmg = Math.Max(1, (int)(damage * chainPct));

            var enemyBoard = ctx.GetOpponentBoard(attackerSide);
            int targetSlot = -1;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = enemyBoard.GetSlot(i);
                if (p != null && !p.IsDead && p != target)
                { targetSlot = i; break; }
            }
            if (targetSlot < 0) return;

            var chainTarget = enemyBoard.GetSlot(targetSlot)!;
            chainTarget.TakeDamage(chainDmg);
            events.Add(new CombatEvent
            {
                Timestamp = timestamp, Type = CombatEventType.PerkTriggered,
                SourceSide = attackerSide, PerkId = perk.Definition.Id,
                Description = $"ONE_SHOT Arc Battery: chained {source.Definition.Type}({source.Id}) -> {chainTarget.Definition.Type}({chainTarget.Id}) for {chainDmg} dmg"
            });

            if (chainTarget.IsDead)
            {
                ctx.RecordKill(attackerSide, chainTarget.Value);
                events.Add(CombatEvent.Kill(timestamp,
                    attackerSide, -1, source.Definition.Type.ToString(), source.Id,
                    attackerSide == Side.Player ? Side.Enemy : Side.Player, targetSlot,
                    chainTarget.Definition.Type.ToString(), chainTarget.Id,
                    chainDmg, chainTarget.CurrentHp + chainDmg, "arc_battery"));
            }
        }
    }

    public class AllPawnsAscendEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null) continue;
                if (p.Definition.Type != PieceType.Pawn) return;
            }
            float hpMult = perk.Definition.GetFloatParam("hp_mult", 2f);
            float atkMult = perk.Definition.GetFloatParam("atk_mult", 2f);
            float cdMult = perk.Definition.GetFloatParam("cooldown_mult_cond", 0.70f);
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null || p.IsDead) continue;
                p.MaxHp = (int)(p.MaxHp * hpMult);
                p.CurrentHp = p.MaxHp;
                p.Atk = (int)(p.Atk * atkMult);
                p.CooldownMultiplier *= cdMult;
            }
        }
    }

    public class HorsemenEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            if (!Enum.TryParse<PieceType>(perk.Definition.GetStringParam("cond_min_piece_type"), true, out var pt))
                return;
            int minCount = perk.Definition.GetIntParam("cond_min_count", 4);
            int count = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p != null && !p.IsDead && p.Definition.Type == pt) count++;
            }
            if (count < minCount) return;

            float dmgMult = perk.Definition.GetFloatParam("outgoing_damage_mult", 3f);
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p != null && !p.IsDead && p.Definition.Type == pt)
                    p.Atk = (int)(p.Atk * dmgMult);
            }
        }
    }

    public class TwinTowersEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            if (!Enum.TryParse<PieceType>(perk.Definition.GetStringParam("cond_piece_type_extremes"), true, out var pt))
                return;
            var first = board.GetSlot(0);
            var last = board.GetSlot(BoardState.ACTIVE_SLOTS - 1);
            if (first == null || first.Definition.Type != pt) return;
            if (last == null || last.Definition.Type != pt) return;

            int atkBonus = perk.Definition.GetIntParam("add_atk_bonus", 2);
            float hpMult = perk.Definition.GetFloatParam("hp_mult", 3f);
            foreach (var p in new[] { first, last })
            {
                p.Atk += atkBonus;
                p.MaxHp = (int)(p.MaxHp * hpMult);
                p.CurrentHp = p.MaxHp;
            }
        }
    }

    public class RoyalGuardEffect : IPerkEffect
    {
        private readonly Dictionary<string, bool> _active = new();

        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            bool hasQueen = false, hasKing = false;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null || p.IsDead) continue;
                if (p.Definition.Type == PieceType.Queen) hasQueen = true;
                if (p.Definition.Type == PieceType.King) hasKing = true;
            }
            string key = $"{perk.Definition.Id}_{side}";
            _active[key] = hasQueen && hasKing;
        }

        public float ModifyIncomingDamage(CombatContext ctx, PerkInstance perk,
            PieceInstance source, PieceInstance target, float damage)
        {
            Side defSide = ctx.PlayerBoard.ContainsPiece(target) ? Side.Player : Side.Enemy;
            string key = $"{perk.Definition.Id}_{defSide}";
            if (!_active.TryGetValue(key, out bool active) || !active) return damage;
            if (target.Definition.Type != PieceType.Queen && target.Definition.Type != PieceType.King)
                return damage;

            var board = ctx.GetBoard(defSide);
            bool othersDead = true;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null || p.IsDead) continue;
                if (p.Definition.Type != PieceType.Queen && p.Definition.Type != PieceType.King)
                { othersDead = false; break; }
            }
            if (othersDead) { _active[key] = false; return damage; }

            float reductionPct = perk.Definition.GetFloatParam("damage_reduction_pct", 0.75f);
            return damage * (1f - reductionPct);
        }

        public void OnTick(CombatContext ctx, PerkInstance perk, BoardState board, Side side,
            float elapsed, List<CombatEvent> events, IRandomService rng)
        {
            string key = $"{perk.Definition.Id}_{side}";
            if (!_active.TryGetValue(key, out bool active) || !active) return;
            float halfDuration = ctx.CombatDuration * 0.5f;
            if (elapsed >= halfDuration)
                _active[key] = false;
        }
    }

    public class BishopCommunionEffect : IPerkEffect
    {
        private readonly HashSet<string> _active = new();

        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            if (!Enum.TryParse<PieceType>(perk.Definition.GetStringParam("cond_min_piece_type"), true, out var pt))
                return;
            int minCount = perk.Definition.GetIntParam("cond_min_count", 3);
            int count = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p != null && !p.IsDead && p.Definition.Type == pt) count++;
            }
            if (count >= minCount)
                _active.Add($"{perk.Definition.Id}_{side}");
        }

        public void OnDamageDealt(CombatContext ctx, PerkInstance perk, PieceInstance source,
            PieceInstance target, int damage, float timestamp, List<CombatEvent> events)
        {
            Side atkSide = ctx.PlayerBoard.ContainsPiece(source) ? Side.Player : Side.Enemy;
            string key = $"{perk.Definition.Id}_{atkSide}";
            if (!_active.Contains(key)) return;

            float lifestealPct = perk.Definition.GetFloatParam("lifesteal_pct", 0.20f);
            int healAmt = Math.Max(1, (int)(damage * lifestealPct));
            if (source.IsDead) return;
            source.Heal(healAmt);
        }
    }

    public class DoubleTapEffect : IPerkEffect
    {
        public void OnDamageDealt(CombatContext ctx, PerkInstance perk, PieceInstance source,
            PieceInstance target, int damage, float timestamp, List<CombatEvent> events)
        {
            if (source.IsDead || target.IsDead) return;
            if (perk.TargetPieceId != null && perk.TargetPieceId != source.Id) return;

            float chance = perk.Definition.GetFloatParam("double_attack_chance", 0.02f);
            var rng = ctx.CombatRng;
            if (rng == null || rng.NextFloat() >= chance) return;

            int extraDmg = source.EffectiveAtk();
            int hpBefore = target.CurrentHp;
            target.TakeDamage(extraDmg);
            events.Add(new CombatEvent
            {
                Timestamp = timestamp, Type = CombatEventType.PerkTriggered,
                SourceSide = ctx.PlayerBoard.ContainsPiece(source) ? Side.Player : Side.Enemy,
                PerkId = perk.Definition.Id,
                Description = $"PROC Double Tap: extra attack {source.Definition.Type}({source.Id}) -> {target.Definition.Type}({target.Id}) for {extraDmg} dmg ({hpBefore}->{target.CurrentHp})"
            });

            if (target.IsDead)
            {
                Side atkSide = ctx.PlayerBoard.ContainsPiece(source) ? Side.Player : Side.Enemy;
                ctx.RecordKill(atkSide, target.Value);
            }
        }
    }

    public class PawnChainEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            int bonusPerAdj = perk.Definition.GetIntParam("adjacent_atk_bonus", 1) * perk.Stacks;
            bool anyApplied = false;

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null || p.IsDead || p.Definition.Type != PieceType.Pawn) continue;
                int adjCount = 0;
                if (i > 0)
                {
                    var left = board.GetSlot(i - 1);
                    if (left != null && !left.IsDead && left.Definition.Type == PieceType.Pawn) adjCount++;
                }
                if (i < BoardState.ACTIVE_SLOTS - 1)
                {
                    var right = board.GetSlot(i + 1);
                    if (right != null && !right.IsDead && right.Definition.Type == PieceType.Pawn) adjCount++;
                }
                if (adjCount > 0)
                {
                    p.BonusAtk += adjCount * bonusPerAdj;
                    p.Atk += adjCount * bonusPerAdj;
                    anyApplied = true;
                }
            }
        }
    }

    public class ThornsEffect : IPerkEffect
    {
        public float ModifyIncomingDamage(CombatContext ctx, PerkInstance perk,
            PieceInstance source, PieceInstance target, float damage)
        {
            float reflectPct = perk.Definition.GetFloatParam("reflect_pct") * perk.Stacks;
            int reflected = (int)(damage * reflectPct);
            if (reflected > 0)
            {
                source.TakeDamage(reflected);
                ctx.LastReflectedDamage += reflected;
            }
            return damage;
        }
    }

    public class StoneSkinEffect : IPerkEffect
    {
        public float ModifyIncomingDamage(CombatContext ctx, PerkInstance perk,
            PieceInstance source, PieceInstance target, float damage)
        {
            float mult = perk.Definition.GetFloatParam("damage_taken_mult", 1f);
            for (int i = 0; i < perk.Stacks; i++)
                damage *= mult;
            return damage;
        }
    }

    public class GiantSlayerEffect : IPerkEffect
    {
        public float ModifyOutgoingDamage(CombatContext ctx, PerkInstance perk,
            PieceInstance source, PieceInstance target, float damage)
        {
            int threshold = perk.Definition.GetIntParam("threshold_hp", 18);
            if (target.MaxHp >= threshold)
            {
                float bonus = perk.Definition.GetFloatParam("bonus_damage_vs_high_hp_pct", 0.12f) * perk.Stacks;
                damage *= (1f + bonus);
            }
            return damage;
        }
    }

    public class MomentumEffect : IPerkEffect
    {
        public void OnPieceKilled(CombatContext ctx, PerkInstance perk,
            PieceInstance killer, PieceInstance victim,
            float timestamp, List<CombatEvent> events)
        {
            int addAtk = perk.Definition.GetIntParam("on_kill_add_atk", 1) * perk.Stacks;
            float duration = perk.Definition.GetFloatParam("on_kill_duration_seconds", 6f);
            killer.StatusEffects.Add(new StatusEffect
            {
                Type = StatusType.AtkBuff,
                IntValue = addAtk,
                Duration = duration
            });
        }
    }

    public class FirstStrikeEffect : IPerkEffect
    {
        public void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side)
        {
            float mult = perk.Definition.GetFloatParam("start_cooldown_bonus_mult", 0.75f);
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece == null) continue;
                for (int s = 0; s < perk.Stacks; s++)
                    piece.CooldownTimer *= mult;
            }
        }
    }

    public class TimeDilationEffect : IPerkEffect
    {
        private bool _triggered;

        public void OnTick(CombatContext ctx, PerkInstance perk, BoardState board, Side side,
            float elapsed, List<CombatEvent> events, IRandomService rng)
        {
            if (_triggered) return;
            float triggerAt = perk.Definition.GetFloatParam("at_seconds", 10f);
            if (elapsed < triggerAt) return;

            _triggered = true;
            float cdMult = perk.Definition.GetFloatParam("enemy_cooldown_mult", 1.3f);
            float duration = perk.Definition.GetFloatParam("duration_seconds", 6f);
            var enemyBoard = ctx.GetOpponentBoard(side);

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = enemyBoard.GetSlot(i);
                if (piece == null || piece.IsDead) continue;
                piece.StatusEffects.Add(new StatusEffect
                {
                    Type = StatusType.CooldownSlow,
                    FloatValue = cdMult,
                    Duration = duration
                });
            }

            events.Add(new CombatEvent
            {
                Timestamp = elapsed, Type = CombatEventType.PerkTriggered,
                SourceSide = side, PerkId = perk.Definition.Id,
                Description = $"{perk.Definition.Name}: enemies slowed!"
            });
        }
    }

    public class BlackoutEffect : IPerkEffect
    {
        public void OnPieceKilled(CombatContext ctx, PerkInstance perk,
            PieceInstance killer, PieceInstance victim,
            float timestamp, List<CombatEvent> events)
        {
            float chance = perk.Definition.GetFloatParam("on_enemy_death_chance", 0.35f);
            float stunDur = perk.Definition.GetFloatParam("enemy_stun_seconds", 0.8f);

            var enemyBoard = ctx.GetOpponentBoard(Side.Player);
            var alivePieces = new List<(PieceInstance piece, int slot)>();
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = enemyBoard.GetSlot(i);
                if (p != null && !p.IsDead)
                    alivePieces.Add((p, i));
            }

            if (alivePieces.Count > 0)
            {
                var target = alivePieces[0];
                target.piece.StatusEffects.Add(new StatusEffect
                {
                    Type = StatusType.Stun,
                    Duration = stunDur
                });
            }
        }
    }
}
