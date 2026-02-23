using System.Collections.Generic;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Perks;

namespace ChaosArcadeTower.Infrastructure.Balance
{
    public class BoardPowerService
    {
        private readonly BoardPowerWeights _weights;

        public BoardPowerService(BoardPowerWeights weights)
        {
            _weights = weights;
        }

        public float Calculate(BoardState board, List<PerkInstance> perks)
        {
            float power = 0;
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var piece = board.GetSlot(i);
                if (piece == null) continue;
                power += piece.MaxHp * _weights.HpWeight;
                power += piece.Atk * _weights.AtkWeight;
                if (piece.Cooldown > 0)
                    power += (1f / piece.Cooldown) * _weights.CooldownWeight;
                power += piece.Value * _weights.ValueWeight;
            }

            foreach (var perk in perks)
                power += perk.Definition.PowerScore * perk.Stacks * _weights.PerkPowerMultiplier;

            return power;
        }
    }
}
