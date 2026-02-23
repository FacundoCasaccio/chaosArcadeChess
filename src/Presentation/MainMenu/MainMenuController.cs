using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.MainMenu
{
    public partial class MainMenuController : Control
    {
        private GameStateMachine _gsm = null!;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();

            var playBtn = GetNode<Button>("VBox/PlayButton");
            var rankBtn = GetNode<Button>("VBox/RankingButton");
            var optBtn = GetNode<Button>("VBox/OptionsButton");
            var exitBtn = GetNode<Button>("VBox/ExitButton");

            playBtn.Pressed += OnPlay;
            rankBtn.Pressed += OnRanking;
            optBtn.Pressed += OnOptions;
            exitBtn.Pressed += OnExit;

            var title = GetNode<Label>("VBox/Title");
            title.AddThemeFontSizeOverride("font_size", 48);
        }

        private void OnPlay() => _gsm.StartNewRun();
        private void OnRanking() => _gsm.TransitionTo(GameState.Ranking);
        private void OnOptions() => _gsm.TransitionTo(GameState.Options);
        private void OnExit() => GetTree().Quit();
    }
}
