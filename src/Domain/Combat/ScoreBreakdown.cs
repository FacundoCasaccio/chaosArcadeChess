namespace ChaosArcadeTower.Domain.Combat
{
    public class ScoreBreakdown
    {
        public int AliveAlliesScore { get; set; }
        public int KilledEnemiesScore { get; set; }
        public int EmptySlotHitsScore { get; set; }
        public int PerkBonusScore { get; set; }

        public int Total => AliveAlliesScore + KilledEnemiesScore + EmptySlotHitsScore + PerkBonusScore;
    }
}
