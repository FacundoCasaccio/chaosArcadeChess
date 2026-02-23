using System.Collections.Generic;

namespace ChaosArcadeTower.Domain.Pieces
{
    public class PieceInstance
    {
        public string Id { get; }
        public PieceDefinition Definition { get; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int Atk { get; set; }
        public float Cooldown { get; set; }
        public float CooldownTimer { get; set; }
        public int Value { get; set; }
        public bool IsDead => CurrentHp <= 0;

        public int BonusHp { get; set; }
        public int BonusAtk { get; set; }
        public float CooldownMultiplier { get; set; } = 1f;

        public Enchantment? Enchant { get; set; }
        public List<StatusEffect> StatusEffects { get; } = new();
        public List<string> AppliedPerkIds { get; } = new();

        public float EffectiveCooldown => Cooldown * CooldownMultiplier;

        public PieceInstance(PieceDefinition def, string id)
        {
            Id = id;
            Definition = def;
            MaxHp = def.BaseHp;
            CurrentHp = def.BaseHp;
            Atk = def.BaseAtk;
            Cooldown = def.BaseCooldown;
            Value = def.Value;
        }

        public PieceInstance DeepClone()
        {
            var clone = new PieceInstance(Definition, Id)
            {
                CurrentHp = CurrentHp,
                MaxHp = MaxHp,
                Atk = Atk,
                Cooldown = Cooldown,
                CooldownTimer = CooldownTimer,
                Value = Value,
                BonusHp = BonusHp,
                BonusAtk = BonusAtk,
                CooldownMultiplier = CooldownMultiplier,
                Enchant = Enchant
            };
            clone.AppliedPerkIds.AddRange(AppliedPerkIds);
            return clone;
        }

        public void ApplyBonuses()
        {
            MaxHp = Definition.BaseHp + BonusHp;
            CurrentHp = MaxHp;
            Atk = Definition.BaseAtk + BonusAtk;
            Cooldown = Definition.BaseCooldown;
        }

        public int EffectiveAtk()
        {
            int a = Atk;
            foreach (var status in StatusEffects)
            {
                if (status.Type == StatusType.AtkBuff)
                    a += status.IntValue;
            }
            return a < 0 ? 0 : a;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            CurrentHp -= amount;
            if (CurrentHp < 0) CurrentHp = 0;
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHp += amount;
            if (CurrentHp > MaxHp) CurrentHp = MaxHp;
        }
    }

    public enum Enchantment
    {
        Ice,
        Fire,
        Lightning,
        Rock,
        Darkness,
        Light
    }

    public class StatusEffect
    {
        public StatusType Type { get; set; }
        public float Duration { get; set; }
        public float Elapsed { get; set; }
        public int IntValue { get; set; }
        public float FloatValue { get; set; }
        public bool IsExpired => Elapsed >= Duration;

        public void Tick(float dt) => Elapsed += dt;
    }

    public enum StatusType
    {
        Freeze,
        Burn,
        Stun,
        AtkBuff,
        CooldownSlow,
        DamageReduction
    }
}
