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
            var events = new List<CombatEvent>();
            var ctx = new CombatContext(pBoard, eBoard, pPerks, ePerks, _perkRegistry);

            ApplyPreCombatPerks(ctx, pPerks, Side.Player);
            ApplyPreCombatPerks(ctx, ePerks, Side.Enemy);
            InitializeCooldowns(pBoard, rng);
            InitializeCooldowns(eBoard, rng);

            int totalTicks = (int)(_duration / _tickSeconds);

            for (int tick = 0; tick < totalTicks; tick++)
            {
                float elapsed = tick * _tickSeconds;
                TickStatusEffects(pBoard, eBoard, _tickSeconds, elapsed, events);

                var readyPieces = CollectReadyPieces(pBoard, eBoard, _tickSeconds);
                readyPieces.Sort((a, b) =>
                {
                    int cmp = a.slot.CompareTo(b.slot);
                    if (cmp != 0) return cmp;
                    return a.side.CompareTo(b.side);
                });

                foreach (var (piece, slot, side) in readyPieces)
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
                    ActionResolver.Execute(piece, slot, side, allyBoard, enemBoard, rng, elapsed, events, ctx);
                    piece.CooldownTimer = piece.EffectiveCooldown;
                }

                ProcessTimedPerks(ctx, pPerks, Side.Player, elapsed, events, rng);
                ProcessTimedPerks(ctx, ePerks, Side.Enemy, elapsed, events, rng);
            }

            return BuildResult(ctx, pBoard, eBoard, events, _duration);
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

        private void InitializeCooldowns(BoardState board, IRandomService rng)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece != null)
                    piece.CooldownTimer = piece.EffectiveCooldown;
            }
        }

        private List<(PieceInstance piece, int slot, Side side)> CollectReadyPieces(
            BoardState playerBoard, BoardState enemyBoard, float dt)
        {
            var ready = new List<(PieceInstance, int, Side)>();

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var pp = playerBoard.GetSlot(i);
                if (pp != null && !pp.IsDead)
                {
                    pp.CooldownTimer -= dt;
                    if (pp.CooldownTimer <= 0)
                        ready.Add((pp, i, Side.Player));
                }

                var ep = enemyBoard.GetSlot(i);
                if (ep != null && !ep.IsDead)
                {
                    ep.CooldownTimer -= dt;
                    if (ep.CooldownTimer <= 0)
                        ready.Add((ep, i, Side.Enemy));
                }
            }

            return ready;
        }

        private void TickStatusEffects(BoardState player, BoardState enemy, float dt, float elapsed, List<CombatEvent> events)
        {
            TickBoardStatuses(player, Side.Player, dt, elapsed, events);
            TickBoardStatuses(enemy, Side.Enemy, dt, elapsed, events);
        }

        private void TickBoardStatuses(BoardState board, Side side, float dt, float elapsed, List<CombatEvent> events)
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
                            piece.TakeDamage(burnDmg);
                            events.Add(new CombatEvent
                            {
                                Timestamp = elapsed, Type = CombatEventType.BurnTick,
                                TargetSide = side, TargetSlot = i, Amount = burnDmg
                            });
                        }
                    }

                    if (status.IsExpired)
                        piece.StatusEffects.RemoveAt(s);
                }
            }
        }

        private void ProcessTimedPerks(CombatContext ctx, List<PerkInstance> perks, Side side, float elapsed, List<CombatEvent> events, IRandomService rng)
        {
            foreach (var perk in perks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                effect?.OnTick(ctx, perk, ctx.GetBoard(side), side, elapsed, events, rng);
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
