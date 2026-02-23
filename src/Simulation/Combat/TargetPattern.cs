using System.Collections.Generic;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.Simulation.Combat
{
    public enum PickMode { All, RandomOne }

    public class AttackGroup
    {
        public int[] Offsets { get; }
        public PickMode Pick { get; }

        public AttackGroup(int[] offsets, PickMode pick)
        {
            Offsets = offsets;
            Pick = pick;
        }
    }

    public class HealGroup
    {
        public AllyTargetMode Mode { get; }
        public int[] AllyOffsets { get; }
        public PickMode Pick { get; }
        public int Amount { get; }

        public HealGroup(AllyTargetMode mode, int[] offsets, PickMode pick, int amount)
        {
            Mode = mode;
            AllyOffsets = offsets;
            Pick = pick;
            Amount = amount;
        }
    }

    public enum AllyTargetMode
    {
        Offsets,
        AllLeft,
        AllRight,
        RandomSide,
        AllPawns
    }

    public class BuffGroup
    {
        public PieceType? TargetType { get; }
        public string Stat { get; }
        public int Amount { get; }
        public float DurationMultiplier { get; }

        public BuffGroup(PieceType? targetType, string stat, int amount, float durationMult = 1f)
        {
            TargetType = targetType;
            Stat = stat;
            Amount = amount;
            DurationMultiplier = durationMult;
        }
    }

    public class PieceActionDef
    {
        public List<AttackGroup> Attacks { get; } = new();
        public List<HealGroup> Heals { get; } = new();
        public List<BuffGroup> Buffs { get; } = new();
    }

    public static class PieceActionRegistry
    {
        private static readonly Dictionary<PieceType, PieceActionDef> _actions = new();

        static PieceActionRegistry()
        {
            _actions[PieceType.Pawn] = new PieceActionDef
            {
                Attacks = { new AttackGroup(new[] { -1, 1 }, PickMode.RandomOne) }
            };

            _actions[PieceType.Knight] = new PieceActionDef
            {
                Attacks =
                {
                    new AttackGroup(new[] { 0 }, PickMode.All),
                    new AttackGroup(new[] { -1, 1 }, PickMode.RandomOne)
                }
            };

            _actions[PieceType.Bishop] = new PieceActionDef
            {
                Attacks = { new AttackGroup(new[] { -1, 1 }, PickMode.All) },
                Heals = { new HealGroup(AllyTargetMode.Offsets, new[] { -1, 1 }, PickMode.RandomOne, 1) }
            };

            _actions[PieceType.Rook] = new PieceActionDef
            {
                Attacks = { new AttackGroup(new[] { 0 }, PickMode.All) },
                Heals = { new HealGroup(AllyTargetMode.RandomSide, System.Array.Empty<int>(), PickMode.All, 1) }
            };

            _actions[PieceType.Queen] = new PieceActionDef
            {
                Attacks = { new AttackGroup(new[] { -1, 0, 1 }, PickMode.All) }
            };

            _actions[PieceType.King] = new PieceActionDef
            {
                Attacks = { new AttackGroup(new[] { 0 }, PickMode.All) },
                Heals = { new HealGroup(AllyTargetMode.Offsets, new[] { -1, 1 }, PickMode.All, 1) },
                Buffs = { new BuffGroup(PieceType.Pawn, "atk", 1, 1f) }
            };
        }

        public static PieceActionDef Get(PieceType type) => _actions[type];
    }
}
