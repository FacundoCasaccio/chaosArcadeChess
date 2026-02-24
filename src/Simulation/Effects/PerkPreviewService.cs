using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Simulation.Combat;

namespace ChaosArcadeTower.Simulation.Effects
{
    /// <summary>
    /// Produces a read-only preview of a board with all OnCombatStart perk
    /// effects applied.  Does NOT mutate the source board or perks.
    /// Godot-free — safe to call from UI or tests.
    /// </summary>
    public static class PerkPreviewService
    {
        public static BoardState PreviewBoard(
            BoardState board, List<PerkInstance> perks, PerkEffectRegistry registry)
        {
            var preview = board.DeepClone();
            var perkClones = perks.Select(p => p.DeepClone()).ToList();

            var dummyOpponent = new BoardState();
            var ctx = new CombatContext(preview, dummyOpponent, perkClones, new List<PerkInstance>(), registry);

            foreach (var perk in perkClones)
            {
                var effect = registry.GetEffect(perk.Definition);
                effect?.OnCombatStart(ctx, perk, preview, Side.Player);
            }

            return preview;
        }
    }
}
