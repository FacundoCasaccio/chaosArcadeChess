using System;
using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Simulation.Combat;
using ChaosArcadeTower.Simulation.Effects;

namespace ChaosArcadeTower.Tests.Simulation
{
    /// <summary>
    /// Pure-C# tests (no Godot dependency).
    /// Call CombatDeterminismTests.RunAll() from an editor script or
    /// any entry point to verify combat invariants.
    /// </summary>
    public static class CombatDeterminismTests
    {
        private const float DURATION = 30f;
        private const float TICK = 0.10f;
        private const int EMPTY_SLOT_PTS = 1;

        public static void RunAll()
        {
            SameSeedProducesSameResult();
            DeadActorNeverActsLaterInSameTick();
            InitialStaggerProducesSequentialFirstActions();
            Console.WriteLine("[CombatDeterminismTests] All tests passed.");
        }

        /// <summary>
        /// Same seed, same boards => identical event log and scores.
        /// </summary>
        public static void SameSeedProducesSameResult()
        {
            var (pBoard, eBoard) = MakeMirrorBoards();
            var resolver = MakeResolver();
            int seed = 42;

            var r1 = resolver.Resolve(pBoard, eBoard, new(), new(), seed);
            var r2 = resolver.Resolve(pBoard, eBoard, new(), new(), seed);

            Assert(r1.EventLog.Count == r2.EventLog.Count,
                $"Event count mismatch: {r1.EventLog.Count} vs {r2.EventLog.Count}");

            for (int i = 0; i < r1.EventLog.Count; i++)
            {
                var a = r1.EventLog[i];
                var b = r2.EventLog[i];
                Assert(a.Type == b.Type && a.SourceSlot == b.SourceSlot &&
                       a.TargetSlot == b.TargetSlot && a.Amount == b.Amount &&
                       Math.Abs(a.Timestamp - b.Timestamp) < 0.001f,
                    $"Event {i} differs: [{a.Timestamp:F2} {a.Type}] vs [{b.Timestamp:F2} {b.Type}]");
            }

            Assert(r1.PlayerScore.Total == r2.PlayerScore.Total,
                $"Player score mismatch: {r1.PlayerScore.Total} vs {r2.PlayerScore.Total}");
            Assert(r1.EnemyScore.Total == r2.EnemyScore.Total,
                $"Enemy score mismatch: {r1.EnemyScore.Total} vs {r2.EnemyScore.Total}");

            Console.WriteLine("  [PASS] SameSeedProducesSameResult");
        }

        /// <summary>
        /// Place a very weak piece (1 HP) that will die on the first enemy
        /// action.  Verify it never appears as an actor in any later event
        /// on the same tick.
        /// </summary>
        public static void DeadActorNeverActsLaterInSameTick()
        {
            var pBoard = new BoardState();
            var eBoard = new BoardState();

            var pawnDef = new PieceDefinition(PieceType.Pawn, 1, 1, 2, 1.20f, 1);
            var strongDef = new PieceDefinition(PieceType.Queen, 4, 100, 50, 1.20f, 4);

            // Player slot 0: strong queen that will die to nothing (she attacks first)
            // Player slot 1: 1-HP pawn that should die quickly
            pBoard.SetSlot(0, new PieceInstance(strongDef, "p_0"));
            pBoard.SetSlot(1, new PieceInstance(pawnDef, "p_1"));

            // Enemy slot 0: strong queen that attacks diag-right (+1) or diag-left (-1)
            // Enemy slot 1: strong queen
            eBoard.SetSlot(0, new PieceInstance(strongDef, "bot_0"));
            eBoard.SetSlot(1, new PieceInstance(strongDef, "bot_1"));

            var resolver = MakeResolver();
            var result = resolver.Resolve(pBoard, eBoard, new(), new(), seed: 123);

            // Find the tick when p_1 dies
            float? deathTick = null;
            foreach (var evt in result.EventLog)
            {
                if (evt.Type == CombatEventType.PieceKilled &&
                    evt.TargetSide == Side.Player && evt.TargetSlot == 1)
                {
                    deathTick = evt.Timestamp;
                    break;
                }
            }

            if (deathTick.HasValue)
            {
                // Verify p_1 never acts as a source AFTER the same timestamp
                foreach (var evt in result.EventLog)
                {
                    if (evt.Timestamp < deathTick.Value) continue;
                    if (evt.Type == CombatEventType.Damage ||
                        evt.Type == CombatEventType.Heal ||
                        evt.Type == CombatEventType.EmptySlotHit)
                    {
                        bool deadPieceActed = evt.SourceSide == Side.Player && evt.SourceSlot == 1;
                        Assert(!deadPieceActed,
                            $"Dead piece p_1 acted at t={evt.Timestamp:F2} (died at t={deathTick.Value:F2}): {evt.ToLogString()}");
                    }
                }
            }

            Console.WriteLine("  [PASS] DeadActorNeverActsLaterInSameTick");
        }

        /// <summary>
        /// With identical cooldowns, the initial stagger must cause slot 0
        /// to fire strictly before slot 1 (i.e. at an earlier timestamp).
        /// </summary>
        public static void InitialStaggerProducesSequentialFirstActions()
        {
            var pBoard = new BoardState();
            var eBoard = new BoardState();

            var pawnDef = new PieceDefinition(PieceType.Pawn, 1, 10, 2, 1.20f, 1);

            for (int i = 0; i < 3; i++)
            {
                pBoard.SetSlot(i, new PieceInstance(pawnDef, $"p_{i}"));
                eBoard.SetSlot(i, new PieceInstance(pawnDef, $"bot_{i}"));
            }

            var resolver = MakeResolver();
            var result = resolver.Resolve(pBoard, eBoard, new(), new(), seed: 99);

            var firstActionTimes = new Dictionary<string, float>();
            foreach (var evt in result.EventLog)
            {
                if (evt.Type != CombatEventType.Damage &&
                    evt.Type != CombatEventType.EmptySlotHit) continue;

                string key = $"{evt.SourceSide}_{evt.SourceSlot}";
                if (!firstActionTimes.ContainsKey(key))
                    firstActionTimes[key] = evt.Timestamp;
            }

            if (firstActionTimes.TryGetValue("Player_0", out float t0) &&
                firstActionTimes.TryGetValue("Player_1", out float t1) &&
                firstActionTimes.TryGetValue("Player_2", out float t2))
            {
                Assert(t0 < t1, $"Slot 0 ({t0:F2}) should fire before slot 1 ({t1:F2})");
                Assert(t1 < t2, $"Slot 1 ({t1:F2}) should fire before slot 2 ({t2:F2})");
            }
            else
            {
                Assert(false, "Expected at least 3 player pieces to fire");
            }

            Console.WriteLine("  [PASS] InitialStaggerProducesSequentialFirstActions");
        }

        // -- helpers --

        private static CombatResolver MakeResolver()
        {
            return new CombatResolver(DURATION, TICK, EMPTY_SLOT_PTS, new PerkEffectRegistry());
        }

        private static (BoardState player, BoardState enemy) MakeMirrorBoards()
        {
            var p = new BoardState();
            var e = new BoardState();
            var pawnDef = new PieceDefinition(PieceType.Pawn, 1, 10, 2, 1.20f, 1);
            var knightDef = new PieceDefinition(PieceType.Knight, 2, 14, 3, 1.40f, 2);
            var rookDef = new PieceDefinition(PieceType.Rook, 3, 18, 4, 1.60f, 3);

            p.SetSlot(0, new PieceInstance(pawnDef, "p_0"));
            p.SetSlot(1, new PieceInstance(knightDef, "p_1"));
            p.SetSlot(2, new PieceInstance(rookDef, "p_2"));
            p.SetSlot(3, new PieceInstance(pawnDef, "p_3"));
            p.SetSlot(4, new PieceInstance(knightDef, "p_4"));

            e.SetSlot(0, new PieceInstance(pawnDef, "bot_0"));
            e.SetSlot(1, new PieceInstance(knightDef, "bot_1"));
            e.SetSlot(2, new PieceInstance(rookDef, "bot_2"));
            e.SetSlot(3, new PieceInstance(pawnDef, "bot_3"));
            e.SetSlot(4, new PieceInstance(knightDef, "bot_4"));

            return (p, e);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[FAIL] {message}");
        }
    }
}
