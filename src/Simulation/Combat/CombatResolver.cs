using System;
using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Simulation.Effects;

namespace ChaosArcadeTower.Simulation.Combat
{
    public interface ICombatResolver
    {
        CombatResult Resolve(
            BoardState playerBoard, BoardState enemyBoard,
            List<PerkInstance> playerPerks, List<PerkInstance> enemyPerks,
            int seed);
    }

    /// <summary>
    /// Deterministic tick-based combat.
    ///
    /// Resolution order per tick:
    ///   1. Status effects tick (burn damage, expiry).
    ///   2. All alive pieces' cooldowns are advanced by tickSeconds.
    ///   3. Pieces whose cooldownTimer &lt;= 0 are collected as ready actors.
    ///   4. Ready actors are sorted by (slotIndex ASC, piece.Id ASC) for
    ///      stable, deterministic ordering.
    ///   5. Each ready actor is resolved one-by-one.  Before executing,
    ///      the actor's alive state is re-checked (it may have been killed
    ///      by a prior action in the same tick).
    ///   6. Timed perk effects are processed.
    ///
    /// Initial cooldowns are staggered by slotIndex * tickSeconds so the
    /// first wave of actions resolves sequentially rather than as one burst.
    /// </summary>
    public class CombatResolver : ICombatResolver
    {
        private readonly float _duration;
        private readonly float _tickSeconds;
        private readonly int _emptySlotPoints;
        private readonly PerkEffectRegistry _perkRegistry;

        public CombatResolver(float duration, float tickSeconds, int emptySlotPoints, PerkEffectRegistry perkRegistry)
        {
            _duration = duration;
            _tickSeconds = tickSeconds;
            _emptySlotPoints = emptySlotPoints;
            _perkRegistry = perkRegistry;
        }

        public CombatResult Resolve(
            BoardState playerBoard, BoardState enemyBoard,
            List<PerkInstance> playerPerks, List<PerkInstance> enemyPerks,
            int seed)
        {
            var pBoard = playerBoard.DeepClone();
            var eBoard = enemyBoard.DeepClone();
            var pPerks = playerPerks.Select(p => p.DeepClone()).ToList();
            var ePerks = enemyPerks.Select(p => p.DeepClone()).ToList();
            var rng = new SeededRandomService(seed);
            var sink = new ListCombatEventSink();
            var ctx = new CombatContext(pBoard, eBoard, pPerks, ePerks, _perkRegistry);

            ApplyPreCombatPerks(ctx, pPerks, Side.Player);
            ApplyPreCombatPerks(ctx, ePerks, Side.Enemy);
            InitializeCooldowns(pBoard);
            InitializeCooldowns(eBoard);

            int totalTicks = (int)(_duration / _tickSeconds);

            for (int tick = 0; tick < totalTicks; tick++)
            {
                float elapsed = tick * _tickSeconds;

                TickStatusEffects(pBoard, eBoard, _tickSeconds, elapsed, sink, ctx);

                AdvanceCooldowns(pBoard, _tickSeconds);
                AdvanceCooldowns(eBoard, _tickSeconds);

                var readyActors = CollectReadyActors(pBoard, eBoard);

                readyActors.Sort((a, b) =>
                {
                    int cmp = a.slot.CompareTo(b.slot);
                    if (cmp != 0) return cmp;
                    return string.Compare(a.piece.Id, b.piece.Id, StringComparison.Ordinal);
                });

                foreach (var (piece, slot, side) in readyActors)
                {
                    if (piece.IsDead) continue;

                    bool isStunned = piece.StatusEffects.Any(s => s.Type == StatusType.Stun && !s.IsExpired);
                    bool isFrozen = piece.StatusEffects.Any(s => s.Type == StatusType.Freeze && !s.IsExpired);

                    if (isStunned || isFrozen)
                    {
                        piece.CooldownTimer = piece.EffectiveCooldown;
                        continue;
                    }

                    var allyBoard = side == Side.Player ? pBoard : eBoard;
                    var enemBoard = side == Side.Player ? eBoard : pBoard;
                    ActionResolver.Execute(piece, slot, side, allyBoard, enemBoard, rng, elapsed, sink, ctx);
                    piece.CooldownTimer = piece.EffectiveCooldown;
                }

                ProcessTimedPerks(ctx, pPerks, Side.Player, elapsed, sink, rng);
                ProcessTimedPerks(ctx, ePerks, Side.Enemy, elapsed, sink, rng);
            }

            return BuildResult(ctx, pBoard, eBoard, sink.EventList, _duration);
        }

        private void ApplyPreCombatPerks(CombatContext ctx, List<PerkInstance> perks, Side side)
        {
            var board = ctx.GetBoard(side);
            foreach (var perk in perks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                effect?.OnCombatStart(ctx, perk, board, side);
            }
        }

        /// <summary>
        /// Stagger initial cooldowns by slotIndex * tickSeconds so pieces
        /// fire sequentially (slot 0 first) instead of all at once.
        /// </summary>
        private void InitializeCooldowns(BoardState board)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece != null)
                    piece.CooldownTimer = piece.EffectiveCooldown + i * _tickSeconds;
            }
        }

        private static void AdvanceCooldowns(BoardState board, float dt)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece != null && !piece.IsDead)
                    piece.CooldownTimer -= dt;
            }
        }

        private static List<(PieceInstance piece, int slot, Side side)> CollectReadyActors(
            BoardState playerBoard, BoardState enemyBoard)
        {
            var ready = new List<(PieceInstance, int, Side)>();

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var pp = playerBoard.GetSlot(i);
                if (pp != null && !pp.IsDead && pp.CooldownTimer <= 0)
                    ready.Add((pp, i, Side.Player));

                var ep = enemyBoard.GetSlot(i);
                if (ep != null && !ep.IsDead && ep.CooldownTimer <= 0)
                    ready.Add((ep, i, Side.Enemy));
            }

            return ready;
        }

        private void TickStatusEffects(BoardState player, BoardState enemy, float dt, float elapsed,
            ICombatEventSink sink, CombatContext ctx)
        {
            TickBoardStatuses(player, Side.Player, Side.Enemy, dt, elapsed, sink, ctx);
            TickBoardStatuses(enemy, Side.Enemy, Side.Player, dt, elapsed, sink, ctx);
        }

        private void TickBoardStatuses(BoardState board, Side side, Side oppSide, float dt, float elapsed,
            ICombatEventSink sink, CombatContext ctx)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece == null || piece.IsDead) continue;

                for (int s = piece.StatusEffects.Count - 1; s >= 0; s--)
                {
                    var status = piece.StatusEffects[s];
                    status.Tick(dt);

                    if (status.Type == StatusType.Burn && !status.IsExpired)
                    {
                        int burnDmg = (int)(status.FloatValue * dt);
                        if (burnDmg > 0)
                        {
                            int hpBefore = piece.CurrentHp;
                            piece.TakeDamage(burnDmg);
                            int hpAfter = piece.CurrentHp;

                            sink.Push(new CombatEvent
                            {
                                Timestamp = elapsed, Type = CombatEventType.BurnTick,
                                TargetSide = side, TargetSlot = i, Amount = burnDmg,
                                TargetPieceType = piece.Definition.Type.ToString(),
                                TargetPieceId = piece.Id,
                                TargetHpBefore = hpBefore, TargetHpAfter = hpAfter
                            });

                            if (piece.IsDead)
                            {
                                sink.Push(CombatEvent.Kill(elapsed,
                                    side, -1, null, null,
                                    side, i, piece.Definition.Type.ToString(), piece.Id,
                                    burnDmg, hpBefore, "burn"));
                                ctx.RecordKill(oppSide, piece.Value);
                            }
                        }
                    }

                    if (status.IsExpired)
                        piece.StatusEffects.RemoveAt(s);
                }
            }
        }

        private void ProcessTimedPerks(CombatContext ctx, List<PerkInstance> perks, Side side, float elapsed,
            ICombatEventSink sink, IRandomService rng)
        {
            var eventList = (sink as ListCombatEventSink)?.EventList ?? new List<CombatEvent>();
            foreach (var perk in perks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                effect?.OnTick(ctx, perk, ctx.GetBoard(side), side, elapsed, eventList, rng);
            }
        }

        private CombatResult BuildResult(CombatContext ctx, BoardState pBoard, BoardState eBoard, List<CombatEvent> events, float duration)
        {
            var playerScore = new ScoreBreakdown
            {
                AliveAlliesScore = SumAliveValue(pBoard),
                KilledEnemiesScore = ctx.PlayerKillValue,
                EmptySlotHitsScore = ctx.PlayerEmptySlotHits * _emptySlotPoints,
                PerkBonusScore = ctx.PlayerPerkBonus
            };

            var enemyScore = new ScoreBreakdown
            {
                AliveAlliesScore = SumAliveValue(eBoard),
                KilledEnemiesScore = ctx.EnemyKillValue,
                EmptySlotHitsScore = ctx.EnemyEmptySlotHits * _emptySlotPoints,
                PerkBonusScore = ctx.EnemyPerkBonus
            };

            return new CombatResult
            {
                PlayerScore = playerScore,
                EnemyScore = enemyScore,
                EventLog = events,
                FinalPlayerBoard = pBoard,
                FinalEnemyBoard = eBoard,
                DurationSeconds = duration
            };
        }

        private int SumAliveValue(BoardState board)
        {
            int total = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = board.GetSlot(i);
                if (p != null && !p.IsDead)
                    total += p.Value;
            }
            return total;
        }
    }
}
