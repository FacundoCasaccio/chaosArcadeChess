using System.Collections.Generic;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Simulation.Effects;

namespace ChaosArcadeTower.Simulation.Combat
{
    public class CombatContext
    {
        public BoardState PlayerBoard { get; }
        public BoardState EnemyBoard { get; }
        public List<PerkInstance> PlayerPerks { get; }
        public List<PerkInstance> EnemyPerks { get; }
        public int PlayerEmptySlotHits { get; private set; }
        public int EnemyEmptySlotHits { get; private set; }
        public int PlayerKillValue { get; private set; }
        public int EnemyKillValue { get; private set; }
        public int PlayerPerkBonus { get; set; }
        public int EnemyPerkBonus { get; set; }

        private readonly PerkEffectRegistry _perkRegistry;

        public CombatContext(
            BoardState playerBoard, BoardState enemyBoard,
            List<PerkInstance> playerPerks, List<PerkInstance> enemyPerks,
            PerkEffectRegistry perkRegistry)
        {
            PlayerBoard = playerBoard;
            EnemyBoard = enemyBoard;
            PlayerPerks = playerPerks;
            EnemyPerks = enemyPerks;
            _perkRegistry = perkRegistry;
        }

        public void RecordEmptySlotHit(Side side)
        {
            if (side == Side.Player) PlayerEmptySlotHits++;
            else EnemyEmptySlotHits++;
        }

        public void RecordKill(Side killerSide, int victimValue)
        {
            if (killerSide == Side.Player) PlayerKillValue += victimValue;
            else EnemyKillValue += victimValue;
        }

        public BoardState GetBoard(Side side) => side == Side.Player ? PlayerBoard : EnemyBoard;
        public BoardState GetOpponentBoard(Side side) => side == Side.Player ? EnemyBoard : PlayerBoard;
        public List<PerkInstance> GetPerks(Side side) => side == Side.Player ? PlayerPerks : EnemyPerks;

        public float ModifyOutgoingDamage(PieceInstance source, PieceInstance target, float baseDmg)
        {
            float dmg = baseDmg;
            foreach (var perk in PlayerPerks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                if (effect != null)
                    dmg = effect.ModifyOutgoingDamage(this, perk, source, target, dmg);
            }
            return dmg;
        }

        public float ModifyIncomingDamage(PieceInstance source, PieceInstance target, float baseDmg)
        {
            float dmg = baseDmg;
            foreach (var perk in PlayerPerks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                if (effect != null)
                    dmg = effect.ModifyIncomingDamage(this, perk, source, target, dmg);
            }
            foreach (var perk in EnemyPerks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                if (effect != null)
                    dmg = effect.ModifyIncomingDamage(this, perk, source, target, dmg);
            }
            return dmg;
        }

        public void InvokeDamageDealt(Side atkSide, int atkSlot, PieceInstance attacker,
            Side defSide, int defSlot, PieceInstance defender, int dmg,
            float timestamp, List<CombatEvent> events)
        {
            var perks = GetPerks(atkSide);
            foreach (var perk in perks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                effect?.OnDamageDealt(this, perk, attacker, defender, dmg, timestamp, events);
            }
        }

        public void InvokePieceKilled(Side killerSide, int killerSlot, PieceInstance killer,
            Side victimSide, int victimSlot, PieceInstance victim,
            float timestamp, List<CombatEvent> events)
        {
            var perks = GetPerks(killerSide);
            foreach (var perk in perks)
            {
                var effect = _perkRegistry.GetEffect(perk.Definition);
                effect?.OnPieceKilled(this, perk, killer, victim, timestamp, events);
            }
        }

        public float GetEnchantParam(PieceInstance piece, string key, float defaultVal)
        {
            if (piece.Enchant == null) return defaultVal;
            var perks = piece.AppliedPerkIds.Count > 0 ? PlayerPerks : EnemyPerks;
            foreach (var perk in perks)
            {
                if (perk.Definition.Type == PerkType.Enchant)
                {
                    return perk.Definition.GetFloatParam(key, defaultVal);
                }
            }
            return defaultVal;
        }
    }
}
