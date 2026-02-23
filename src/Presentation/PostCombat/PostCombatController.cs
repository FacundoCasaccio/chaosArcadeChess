using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.PostCombat
{
    public partial class PostCombatController : Control
    {
        private GameStateMachine _gsm = null!;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            BuildUI();
        }

        private void BuildUI()
        {
            var result = _gsm.LastCombatResult;
            if (result == null) return;

            bool won = result.PlayerWon;
            var pScore = result.PlayerScore;
            var eScore = result.EnemyScore;

            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 16);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(vbox);

            var titleLabel = new Label
            {
                Text = won ? "Victory!" : "Defeat",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 40);
            titleLabel.AddThemeColorOverride("font_color", won ? Colors.Green : Colors.Red);
            vbox.AddChild(titleLabel);

            var scorePanel = new PanelContainer();
            var scoreVBox = new VBoxContainer();
            scoreVBox.AddThemeConstantOverride("separation", 8);

            scoreVBox.AddChild(MakeRow("Pieces standing", pScore.AliveAlliesScore));
            scoreVBox.AddChild(MakeRow("Defeated pieces", pScore.KilledEnemiesScore));
            scoreVBox.AddChild(MakeRow("Direct damage", pScore.EmptySlotHitsScore));
            scoreVBox.AddChild(MakeRow("Perks bonus", pScore.PerkBonusScore));
            scoreVBox.AddChild(new HSeparator());
            var totalRow = MakeRow("Total", pScore.Total);
            totalRow.GetChild<Label>(1).AddThemeFontSizeOverride("font_size", 28);
            scoreVBox.AddChild(totalRow);

            scoreVBox.AddChild(new HSeparator());
            scoreVBox.AddChild(MakeRow("Enemy Total", eScore.Total));

            if (won && _gsm.CurrentRun != null)
            {
                int newTotal = _gsm.CurrentRun.TotalScore + pScore.Total;
                scoreVBox.AddChild(new HSeparator());
                scoreVBox.AddChild(MakeRow("Run Score", newTotal));
            }

            scorePanel.AddChild(scoreVBox);
            vbox.AddChild(scorePanel);

            var btnBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            btnBar.AddThemeConstantOverride("separation", 20);

            var backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(140, 50) };
            backBtn.Pressed += () => _gsm.TransitionTo(GameState.Combat);
            btnBar.AddChild(backBtn);

            var continueBtn = new Button { Text = "Continue", CustomMinimumSize = new Vector2(140, 50) };
            continueBtn.Pressed += OnContinue;
            btnBar.AddChild(continueBtn);

            vbox.AddChild(btnBar);
        }

        private HBoxContainer MakeRow(string label, int value)
        {
            var row = new HBoxContainer();
            var lbl = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var val = new Label { Text = value.ToString(), HorizontalAlignment = HorizontalAlignment.Right };
            val.AddThemeFontSizeOverride("font_size", 22);
            row.AddChild(lbl);
            row.AddChild(val);
            return row;
        }

        private void OnContinue() => _gsm.OnPostCombatContinue();
    }
}
