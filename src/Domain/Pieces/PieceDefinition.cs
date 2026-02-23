namespace ChaosArcadeTower.Domain.Pieces
{
    public sealed class PieceDefinition
    {
        public PieceType Type { get; }
        public int Tier { get; }
        public int BaseHp { get; }
        public int BaseAtk { get; }
        public float BaseCooldown { get; }
        public int Value { get; }

        public PieceDefinition(PieceType type, int tier, int hp, int atk, float cooldown, int value)
        {
            Type = type;
            Tier = tier;
            BaseHp = hp;
            BaseAtk = atk;
            BaseCooldown = cooldown;
            Value = value;
        }
    }
}
