using Godot;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.Presentation.Shared
{
    public partial class PieceSlotView : PanelContainer
    {
        private Label _nameLabel = null!;
        private ProgressBar _hpBar = null!;
        private ProgressBar _cdBar = null!;
        private Label _statsLabel = null!;
        private PieceInstance? _piece;

        public int SlotIndex { get; set; }

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(110, 120);

            var vbox = new VBoxContainer();
            _nameLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            vbox.AddChild(_nameLabel);

            _hpBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0, 14),
                ShowPercentage = false,
                MinValue = 0, MaxValue = 100
            };
            vbox.AddChild(_hpBar);

            _statsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            vbox.AddChild(_statsLabel);

            _cdBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0, 8),
                ShowPercentage = false,
                MinValue = 0, MaxValue = 100
            };
            vbox.AddChild(_cdBar);

            AddChild(vbox);
        }

        public void SetPiece(PieceInstance? piece)
        {
            _piece = piece;
            Refresh();
        }

        public void Refresh()
        {
            if (_piece == null || _piece.IsDead)
            {
                _nameLabel.Text = $"Slot {SlotIndex + 1}\n[Empty]";
                _hpBar.Value = 0;
                _statsLabel.Text = "";
                _cdBar.Value = 0;
                return;
            }

            _nameLabel.Text = _piece.Definition.Type.ToString();
            _hpBar.Value = (_piece.MaxHp > 0) ? (float)_piece.CurrentHp / _piece.MaxHp * 100 : 0;
            _statsLabel.Text = $"HP:{_piece.CurrentHp}/{_piece.MaxHp} ATK:{_piece.Atk}";

            float cdPct = (_piece.EffectiveCooldown > 0)
                ? (1f - _piece.CooldownTimer / _piece.EffectiveCooldown) * 100f
                : 100f;
            _cdBar.Value = cdPct;
        }
    }
}
