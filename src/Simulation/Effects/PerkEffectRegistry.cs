using System.Collections.Generic;
using ChaosArcadeTower.Domain.Perks;

namespace ChaosArcadeTower.Simulation.Effects
{
    public class PerkEffectRegistry
    {
        private readonly Dictionary<string, IPerkEffect> _effects = new();
        private readonly StatPerkEffect _statEffect = new();
        private readonly GlobalStatPerkEffect _globalStatEffect = new();
        private readonly SlotPerkEffect _slotEffect = new();
        private readonly PieceTypePerkEffect _pieceTypeEffect = new();
        private readonly EnchantPerkEffect _enchantEffect = new();
        private OneShotPerkEffect _oneShotEffect = new();
        private readonly ThornsEffect _thornsEffect = new();
        private readonly StoneSkinEffect _stoneSkinEffect = new();
        private readonly GiantSlayerEffect _giantSlayerEffect = new();
        private readonly MomentumEffect _momentumEffect = new();
        private readonly FirstStrikeEffect _firstStrikeEffect = new();
        private TimeDilationEffect _timeDilationEffect = new();
        private BlackoutEffect _blackoutEffect = new();
        private EmergencyPatchEffect _emergencyPatchEffect = new();
        private ArcBatteryEffect _arcBatteryEffect = new();
        private RoyalGuardEffect _royalGuardEffect = new();
        private BishopCommunionEffect _bishopCommunionEffect = new();

        public PerkEffectRegistry()
        {
            _effects["r_thorns"] = _thornsEffect;
            _effects["e_stone_skin"] = _stoneSkinEffect;
            _effects["e_giant_slayer"] = _giantSlayerEffect;
            _effects["r_enrage_on_kill"] = _momentumEffect;
            _effects["r_first_strike"] = _firstStrikeEffect;
            _effects["u_time_dilation"] = _timeDilationEffect;
            _effects["u_blackout"] = _blackoutEffect;
            _effects["r_emergency_patch"] = _emergencyPatchEffect;
            _effects["e_arc_battery"] = _arcBatteryEffect;
            _effects["u_all_pawns_ascend"] = new AllPawnsAscendEffect();
            _effects["u_horsemen"] = new HorsemenEffect();
            _effects["u_twin_towers"] = new TwinTowersEffect();
            _effects["u_royal_guard"] = _royalGuardEffect;
            _effects["u_bishop_communion"] = _bishopCommunionEffect;
            _effects["e_double_tap"] = new DoubleTapEffect();
            _effects["e_pawn_chain"] = new PawnChainEffect();
        }

        public void ResetCombatState()
        {
            _oneShotEffect = new OneShotPerkEffect();
            _timeDilationEffect = new TimeDilationEffect();
            _blackoutEffect = new BlackoutEffect();
            _emergencyPatchEffect = new EmergencyPatchEffect();
            _arcBatteryEffect = new ArcBatteryEffect();
            _royalGuardEffect = new RoyalGuardEffect();
            _bishopCommunionEffect = new BishopCommunionEffect();
            _effects["u_time_dilation"] = _timeDilationEffect;
            _effects["u_blackout"] = _blackoutEffect;
            _effects["r_emergency_patch"] = _emergencyPatchEffect;
            _effects["e_arc_battery"] = _arcBatteryEffect;
            _effects["u_royal_guard"] = _royalGuardEffect;
            _effects["u_bishop_communion"] = _bishopCommunionEffect;
        }

        public IPerkEffect? GetEffect(PerkDefinition def)
        {
            if (_effects.TryGetValue(def.Id, out var specific))
                return specific;

            return def.Type switch
            {
                PerkType.Stat => _statEffect,
                PerkType.Global => _globalStatEffect,
                PerkType.Slot => _slotEffect,
                PerkType.PieceType => _pieceTypeEffect,
                PerkType.Enchant => _enchantEffect,
                PerkType.OneShot => _oneShotEffect,
                _ => null
            };
        }
    }
}
