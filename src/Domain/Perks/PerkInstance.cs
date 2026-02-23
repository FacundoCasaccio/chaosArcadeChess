namespace ChaosArcadeTower.Domain.Perks
{
    public class PerkInstance
    {
        public PerkDefinition Definition { get; }
        public int Stacks { get; set; }
        public int? TargetSlotIndex { get; set; }
        public string? TargetPieceId { get; set; }
        public int ChargesRemaining { get; set; }
        public bool IsVisible { get; set; } = true;

        public PerkInstance(PerkDefinition def)
        {
            Definition = def;
            Stacks = 1;
            ChargesRemaining = def.Type == PerkType.OneShot ? 1 : -1;
        }

        public bool CanStack => Definition.Stacking == StackingMode.Additive && Stacks < Definition.MaxStacks;

        public PerkInstance DeepClone()
        {
            return new PerkInstance(Definition)
            {
                Stacks = Stacks,
                TargetSlotIndex = TargetSlotIndex,
                TargetPieceId = TargetPieceId,
                ChargesRemaining = ChargesRemaining,
                IsVisible = IsVisible
            };
        }
    }
}
