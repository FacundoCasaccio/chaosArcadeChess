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

    public class ThornsEffect : IPerkEffect
    {
        public float ModifyIncomingDamage(CombatContext ctx, PerkInstance perk,
            PieceInstance source, PieceInstance target, float damage)
        {
            float reflectPct = perk.Definition.GetFloatParam("reflect_pct") * perk.Stacks;
            int reflected = (int)(damage * reflectPct);
            if (reflected > 0)
                source.TakeDamage(reflected);
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
