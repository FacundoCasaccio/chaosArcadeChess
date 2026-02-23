using System.Collections.Generic;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.Simulation.Combat
{
    public static class ActionResolver
    {
        /// <summary>
        /// Execute a single piece's action.  Between each sub-step (each
        /// attack target, each heal target, each buff) the actor's alive
        /// state is rechecked so that reflected/thorns damage that kills
        /// the actor stops the remainder of the action.
        /// </summary>
        public static void Execute(
            PieceInstance actor, int actorSlot, Side actorSide,
            BoardState allyBoard, BoardState enemyBoard,
            IRandomService rng, float timestamp,
            List<CombatEvent> events, CombatContext ctx)
        {
            var actionDef = PieceActionRegistry.Get(actor.Definition.Type);
            var otherSide = actorSide == Side.Player ? Side.Enemy : Side.Player;
            int atk = actor.EffectiveAtk();

            foreach (var atkGroup in actionDef.Attacks)
            {
                var targets = ResolveTargetOffsets(atkGroup.Offsets, atkGroup.Pick, actorSlot, rng);
                foreach (int offset in targets)
                {
                    if (actor.IsDead) return;

                    int targetSlot = actorSlot + offset;
                    if (targetSlot < 0 || targetSlot >= BoardState.ACTIVE_SLOTS) continue;

                    var target = enemyBoard.GetSlot(targetSlot);
                    if (target == null || target.IsDead)
                    {
                        events.Add(CombatEvent.EmptyHit(timestamp, actorSide, actorSlot, otherSide, targetSlot));
                        ctx.RecordEmptySlotHit(actorSide);
                        continue;
                    }

                    int finalDmg = ComputeDamage(atk, actor, target, ctx);
                    target.TakeDamage(finalDmg);
                    events.Add(CombatEvent.Damage(timestamp, actorSide, actorSlot, otherSide, targetSlot, finalDmg));

                    ctx.InvokeDamageDealt(actorSide, actorSlot, actor, otherSide, targetSlot, target, finalDmg, timestamp, events);

                    ApplyEnchantOnHit(actor, target, otherSide, targetSlot, actorSide, actorSlot, timestamp, rng, events, ctx);

                    if (target.IsDead)
                    {
                        events.Add(CombatEvent.Kill(timestamp, actorSide, actorSlot, otherSide, targetSlot));
                        ctx.RecordKill(actorSide, target.Value);
                        ctx.InvokePieceKilled(actorSide, actorSlot, actor, otherSide, targetSlot, target, timestamp, events);
                    }
                }
            }

            if (actor.IsDead) return;

            foreach (var healGroup in actionDef.Heals)
            {
                var healTargets = ResolveHealTargets(healGroup, actorSlot, allyBoard, rng);
                foreach (int slot in healTargets)
                {
                    var ally = allyBoard.GetSlot(slot);
                    if (ally == null || ally.IsDead) continue;
                    ally.Heal(healGroup.Amount);
                    events.Add(CombatEvent.Heal(timestamp, actorSide, actorSlot, actorSide, slot, healGroup.Amount));
                }
            }

            if (actor.IsDead) return;

            foreach (var buffGroup in actionDef.Buffs)
            {
                ApplyBuff(buffGroup, actor, actorSlot, actorSide, allyBoard, timestamp, events);
            }
        }

        private static int ComputeDamage(int baseAtk, PieceInstance source, PieceInstance target, CombatContext ctx)
        {
            float dmg = baseAtk;
            dmg = ctx.ModifyOutgoingDamage(source, target, dmg);
            dmg = ctx.ModifyIncomingDamage(source, target, dmg);
            return dmg < 0 ? 0 : (int)System.Math.Round(dmg);
        }

        private static List<int> ResolveTargetOffsets(int[] offsets, PickMode pick, int actorSlot, IRandomService rng)
        {
            var result = new List<int>();
            if (pick == PickMode.All)
            {
                result.AddRange(offsets);
            }
            else
            {
                var valid = new List<int>();
                foreach (var o in offsets)
                {
                    int slot = actorSlot + o;
                    if (slot >= 0 && slot < BoardState.ACTIVE_SLOTS)
                        valid.Add(o);
                }
                if (valid.Count > 0)
                    result.Add(valid[rng.NextInt(valid.Count)]);
                else if (offsets.Length > 0)
                    result.Add(offsets[rng.NextInt(offsets.Length)]);
            }
            return result;
        }

        private static List<int> ResolveHealTargets(HealGroup group, int actorSlot, BoardState allyBoard, IRandomService rng)
        {
            var targets = new List<int>();
            switch (group.Mode)
            {
                case AllyTargetMode.Offsets:
                    if (group.Pick == PickMode.All)
                    {
                        foreach (var o in group.AllyOffsets)
                        {
                            int s = actorSlot + o;
                            if (s >= 0 && s < BoardState.ACTIVE_SLOTS)
                                targets.Add(s);
                        }
                    }
                    else
                    {
                        var valid = new List<int>();
                        foreach (var o in group.AllyOffsets)
                        {
                            int s = actorSlot + o;
                            if (s >= 0 && s < BoardState.ACTIVE_SLOTS && allyBoard.GetSlot(s) != null && !allyBoard.GetSlot(s)!.IsDead)
                                valid.Add(s);
                        }
                        if (valid.Count > 0)
                            targets.Add(valid[rng.NextInt(valid.Count)]);
                    }
                    break;

                case AllyTargetMode.RandomSide:
                    bool goLeft = rng.NextInt(2) == 0;
                    if (goLeft)
                        for (int i = 0; i < actorSlot; i++) targets.Add(i);
                    else
                        for (int i = actorSlot + 1; i < BoardState.ACTIVE_SLOTS; i++) targets.Add(i);
                    break;

                case AllyTargetMode.AllPawns:
                    for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                    {
                        var p = allyBoard.GetSlot(i);
                        if (p != null && !p.IsDead && p.Definition.Type == PieceType.Pawn)
                            targets.Add(i);
                    }
                    break;
            }
            return targets;
        }

        private static void ApplyBuff(BuffGroup group, PieceInstance actor, int actorSlot, Side side, BoardState board, float timestamp, List<CombatEvent> events)
        {
            float dur = actor.EffectiveCooldown * group.DurationMultiplier;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p == null || p.IsDead) continue;
                if (group.TargetType.HasValue && p.Definition.Type != group.TargetType.Value) continue;
                if (i == actorSlot) continue;

                p.StatusEffects.Add(new StatusEffect
                {
                    Type = StatusType.AtkBuff,
                    IntValue = group.Amount,
                    Duration = dur
                });
            }
        }

        private static void ApplyEnchantOnHit(
            PieceInstance source, PieceInstance target,
            Side targetSide, int targetSlot, Side sourceSide, int sourceSlot,
            float timestamp, IRandomService rng,
            List<CombatEvent> events, CombatContext ctx)
        {
            if (source.Enchant == null) return;

            switch (source.Enchant.Value)
            {
                case Enchantment.Ice:
                    float freezeChance = ctx.GetEnchantParam(source, "chance_on_hit", 0.20f);
                    float freezeDur = ctx.GetEnchantParam(source, "freeze_seconds", 1.0f);
                    if (rng.NextFloat() < freezeChance)
                    {
                        target.StatusEffects.Add(new StatusEffect { Type = StatusType.Freeze, Duration = freezeDur });
                        events.Add(new CombatEvent
                        {
                            Timestamp = timestamp, Type = CombatEventType.StatusApplied,
                            SourceSide = sourceSide, SourceSlot = sourceSlot,
                            TargetSide = targetSide, TargetSlot = targetSlot,
                            Description = "Frozen!"
                        });
                    }
                    break;

                case Enchantment.Fire:
                    float burnChance = ctx.GetEnchantParam(source, "chance_on_hit", 0.25f);
                    float burnDps = ctx.GetEnchantParam(source, "burn_dps", 1f);
                    float burnDur = ctx.GetEnchantParam(source, "burn_seconds", 5f);
                    if (rng.NextFloat() < burnChance)
                    {
                        target.StatusEffects.Add(new StatusEffect
                        {
                            Type = StatusType.Burn, FloatValue = burnDps, Duration = burnDur
                        });
                        events.Add(new CombatEvent
                        {
                            Timestamp = timestamp, Type = CombatEventType.StatusApplied,
                            SourceSide = sourceSide, SourceSlot = sourceSlot,
                            TargetSide = targetSide, TargetSlot = targetSlot,
                            Description = "Burning!"
                        });
                    }
                    break;
            }
        }
    }
}
