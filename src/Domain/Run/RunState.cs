using System.Collections.Generic;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Perks;

namespace ChaosArcadeTower.Domain.Run
{
    public class RunState
    {
        public int Seed { get; set; }
        public int Floor { get; set; } = 1;
        public int Lives { get; set; } = 3;
        public int TotalScore { get; set; }
        public int Wins { get; set; }
        public string PlayerName { get; set; } = "Player";
        public BoardState Board { get; set; } = new();
        public List<PerkInstance> Perks { get; set; } = new();
        public bool IsGameOver => Lives <= 0;

        public void AddScore(int points)
        {
            if (points > 0)
                TotalScore += points;
        }

        public void LoseLife()
        {
            if (Lives > 0)
                Lives--;
        }

        public void AdvanceFloor()
        {
            Floor++;
        }
    }
}
