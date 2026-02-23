namespace ChaosArcadeTower.Domain.Combat
{
    public enum CombatEventType
    {
        Damage,
        Heal,
        PieceKilled,
        EmptySlotHit,
        StatusApplied,
        StatusExpired,
        PerkTriggered,
        BuffApplied,
        BurnTick
    }

    public enum Side
    {
        Player,
        Enemy
    }

    public struct CombatEvent
    {
        public float Timestamp;
        public CombatEventType Type;
        public Side SourceSide;
        public int SourceSlot;
        public Side TargetSide;
        public int TargetSlot;
        public int Amount;
        public string? PerkId;
        public string? Description;

        public static CombatEvent Damage(float t, Side srcSide, int srcSlot, Side tgtSide, int tgtSlot, int dmg)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.Damage,
                SourceSide = srcSide, SourceSlot = srcSlot,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                Amount = dmg
            };
        }

        public static CombatEvent Heal(float t, Side srcSide, int srcSlot, Side tgtSide, int tgtSlot, int amount)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.Heal,
                SourceSide = srcSide, SourceSlot = srcSlot,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                Amount = amount
            };
        }

        public static CombatEvent Kill(float t, Side killerSide, int killerSlot, Side victimSide, int victimSlot)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.PieceKilled,
                SourceSide = killerSide, SourceSlot = killerSlot,
                TargetSide = victimSide, TargetSlot = victimSlot
            };
        }

        public static CombatEvent EmptyHit(float t, Side srcSide, int srcSlot, Side tgtSide, int tgtSlot)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.EmptySlotHit,
                SourceSide = srcSide, SourceSlot = srcSlot,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                Amount = 1
            };
        }

        public string ToLogString()
        {
            string src = $"{SideChar(SourceSide)}{SourceSlot + 1}";
            string tgt = $"{SideChar(TargetSide)}{TargetSlot + 1}";
            return Type switch
            {
                CombatEventType.Damage => $"{src} dealt {Amount} damage to {tgt}",
                CombatEventType.Heal => $"{src} healed {tgt} by {Amount}",
                CombatEventType.PieceKilled => $"{tgt} was defeated by {src}",
                CombatEventType.EmptySlotHit => $"{src} hit empty slot {tgt} (+1 point)",
                CombatEventType.StatusApplied => $"{Description ?? "Status"} applied to {tgt}",
                CombatEventType.BurnTick => $"{tgt} takes {Amount} burn damage",
                CombatEventType.PerkTriggered => Description ?? $"Perk {PerkId} triggered",
                _ => Description ?? Type.ToString()
            };
        }

        private static char SideChar(Side s) => s == Side.Player ? 'A' : 'B';
    }
}
