using System.Collections.Generic;
using ChaosArcadeTower.Domain.Board;

namespace ChaosArcadeTower.Domain.Combat
{
    public class CombatResult
    {
        public ScoreBreakdown PlayerScore { get; set; } = new();
        public ScoreBreakdown EnemyScore { get; set; } = new();
        public List<CombatEvent> EventLog { get; set; } = new();
        public BoardState FinalPlayerBoard { get; set; } = new();
        public BoardState FinalEnemyBoard { get; set; } = new();
        public BoardState InitialPlayerBoard { get; set; } = new();
        public BoardState InitialEnemyBoard { get; set; } = new();
        public bool PlayerWon => PlayerScore.Total > EnemyScore.Total;
        public bool IsDraw => PlayerScore.Total == EnemyScore.Total;
        public float DurationSeconds { get; set; }
    }
}
