using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;

namespace ChaosArcadeTower.Simulation.Scoring
{
    public static class ScoringService
    {
        public static ScoreBreakdown Calculate(BoardState myBoard, BoardState enemyBoard,
            int killedEnemyValue, int emptySlotHits, int emptySlotPoints, int perkBonus)
        {
            int aliveScore = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var p = myBoard.GetSlot(i);
                if (p != null && !p.IsDead)
                    aliveScore += p.Value;
            }

            return new ScoreBreakdown
            {
                AliveAlliesScore = aliveScore,
                KilledEnemiesScore = killedEnemyValue,
                EmptySlotHitsScore = emptySlotHits * emptySlotPoints,
                PerkBonusScore = perkBonus
            };
        }
    }
}
