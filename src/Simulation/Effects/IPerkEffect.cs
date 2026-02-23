using System.Collections.Generic;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Simulation.Combat;

namespace ChaosArcadeTower.Simulation.Effects
{
    public interface IPerkEffect
    {
        void OnCombatStart(CombatContext ctx, PerkInstance perk, BoardState board, Side side) { }
        void OnDamageDealt(CombatContext ctx, PerkInstance perk, PieceInstance source, PieceInstance target, int damage, float timestamp, List<CombatEvent> events) { }
        void OnPieceKilled(CombatContext ctx, PerkInstance perk, PieceInstance killer, PieceInstance victim, float timestamp, List<CombatEvent> events) { }
        void OnTick(CombatContext ctx, PerkInstance perk, BoardState board, Side side, float elapsed, List<CombatEvent> events, IRandomService rng) { }
        float ModifyOutgoingDamage(CombatContext ctx, PerkInstance perk, PieceInstance source, PieceInstance target, float damage) => damage;
        float ModifyIncomingDamage(CombatContext ctx, PerkInstance perk, PieceInstance source, PieceInstance target, float damage) => damage;
    }
}
