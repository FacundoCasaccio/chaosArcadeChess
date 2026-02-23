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

        public string? SourcePieceType;
        public string? SourcePieceId;
        public string? TargetPieceType;
        public string? TargetPieceId;
        public int TargetHpBefore;
        public int TargetHpAfter;

        public static CombatEvent Damage(float t, Side srcSide, int srcSlot,
            string? srcType, string? srcId,
            Side tgtSide, int tgtSlot,
            string? tgtType, string? tgtId,
            int dmg, int hpBefore, int hpAfter)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.Damage,
                SourceSide = srcSide, SourceSlot = srcSlot,
                SourcePieceType = srcType, SourcePieceId = srcId,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                TargetPieceType = tgtType, TargetPieceId = tgtId,
                Amount = dmg,
                TargetHpBefore = hpBefore, TargetHpAfter = hpAfter
            };
        }

        public static CombatEvent Heal(float t, Side srcSide, int srcSlot,
            Side tgtSide, int tgtSlot, int amount)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.Heal,
                SourceSide = srcSide, SourceSlot = srcSlot,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                Amount = amount
            };
        }

        public static CombatEvent HealRich(float t, Side srcSide, int srcSlot,
            string? srcType, string? srcId,
            Side tgtSide, int tgtSlot,
            string? tgtType, string? tgtId,
            int amount, int hpBefore, int hpAfter)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.Heal,
                SourceSide = srcSide, SourceSlot = srcSlot,
                SourcePieceType = srcType, SourcePieceId = srcId,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                TargetPieceType = tgtType, TargetPieceId = tgtId,
                Amount = amount,
                TargetHpBefore = hpBefore, TargetHpAfter = hpAfter
            };
        }

        public static CombatEvent Kill(float t, Side killerSide, int killerSlot,
            string? killerType, string? killerId,
            Side victimSide, int victimSlot,
            string? victimType, string? victimId,
            int lethalDmg, int victimHpBefore, string? source = null)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.PieceKilled,
                SourceSide = killerSide, SourceSlot = killerSlot,
                SourcePieceType = killerType, SourcePieceId = killerId,
                TargetSide = victimSide, TargetSlot = victimSlot,
                TargetPieceType = victimType, TargetPieceId = victimId,
                Amount = lethalDmg,
                TargetHpBefore = victimHpBefore, TargetHpAfter = 0,
                Description = source
            };
        }

        public static CombatEvent EmptyHit(float t, Side srcSide, int srcSlot,
            string? srcType, string? srcId,
            Side tgtSide, int tgtSlot)
        {
            return new CombatEvent
            {
                Timestamp = t, Type = CombatEventType.EmptySlotHit,
                SourceSide = srcSide, SourceSlot = srcSlot,
                SourcePieceType = srcType, SourcePieceId = srcId,
                TargetSide = tgtSide, TargetSlot = tgtSlot,
                Amount = 1
            };
        }

        public string ToLogString()
        {
            string ts = $"[{Timestamp:F2}]";
            string src = FormatPiece(SourceSide, SourceSlot, SourcePieceType);
            string tgt = FormatPiece(TargetSide, TargetSlot, TargetPieceType);

            return Type switch
            {
                CombatEventType.Damage =>
                    $"{ts} ATK  {src} -> {tgt} | {Amount} dmg ({TargetHpBefore}->{TargetHpAfter})",
                CombatEventType.Heal =>
                    $"{ts} HEAL {src} -> {tgt} | +{Amount} ({TargetHpBefore}->{TargetHpAfter})",
                CombatEventType.PieceKilled =>
                    $"{ts} KILL {tgt} by {src} | {Amount} lethal ({TargetHpBefore}->0){(Description != null ? $" [{Description}]" : "")}",
                CombatEventType.EmptySlotHit =>
                    $"{ts} MISS {src} -> {FormatSlot(TargetSide, TargetSlot)} [empty]",
                CombatEventType.BurnTick =>
                    $"{ts} BURN {tgt} | {Amount} dmg ({TargetHpBefore}->{TargetHpAfter})",
                CombatEventType.StatusApplied =>
                    $"{ts} STATUS {Description ?? "?"} on {tgt}",
                CombatEventType.PerkTriggered =>
                    $"{ts} PERK {Description ?? PerkId ?? "?"}",
                _ => $"{ts} {Description ?? Type.ToString()}"
            };
        }

        private static string FormatPiece(Side side, int slot, string? pieceType)
        {
            string s = SideChar(side).ToString();
            string type = pieceType ?? "?";
            return $"{s}{slot + 1} {type}";
        }

        private static string FormatSlot(Side side, int slot) => $"{SideChar(side)}{slot + 1}";

        private static char SideChar(Side s) => s == Side.Player ? 'A' : 'B';
    }
}
