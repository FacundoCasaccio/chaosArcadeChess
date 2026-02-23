using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.AI
{
    public static class BotPositioner
    {
        private const int ITERATIONS = 80;

        public static void Optimize(BoardState board, BoardState opponentBoard, IRandomService rng)
        {
            float bestScore = EvaluateLayout(board, opponentBoard);

            for (int i = 0; i < ITERATIONS; i++)
            {
                int a = rng.NextInt(BoardState.ACTIVE_SLOTS);
                int b = rng.NextInt(BoardState.ACTIVE_SLOTS);
                if (a == b) continue;

                board.SwapSlots(a, b);
                float newScore = EvaluateLayout(board, opponentBoard);

                if (newScore > bestScore)
                    bestScore = newScore;
                else
                    board.SwapSlots(a, b);
            }
        }

        private static float EvaluateLayout(BoardState board, BoardState opponent)
        {
            float score = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var myPiece = board.GetSlot(i);
                if (myPiece == null) continue;

                var action = Simulation.Combat.PieceActionRegistry.Get(myPiece.Definition.Type);
                float dmgPotential = 0;
                float tankValue = 0;

                foreach (var atkGroup in action.Attacks)
                {
                    foreach (int offset in atkGroup.Offsets)
                    {
                        int targetSlot = i + offset;
                        if (targetSlot < 0 || targetSlot >= BoardState.ACTIVE_SLOTS) continue;

                        var enemy = opponent.GetSlot(targetSlot);
                        if (enemy != null && !enemy.IsDead)
                            dmgPotential += myPiece.Atk * (float)enemy.Value / 3f;
                        else
                            dmgPotential += 0.5f;
                    }
                }

                var facing = opponent.GetSlot(i);
                if (facing != null && !facing.IsDead)
                {
                    float expectedDamage = facing.Atk * (30f / facing.Cooldown);
                    tankValue = myPiece.CurrentHp > expectedDamage * 0.5f ? 2f : -1f;
                }

                score += dmgPotential + tankValue;
            }
            return score;
        }
    }
}
