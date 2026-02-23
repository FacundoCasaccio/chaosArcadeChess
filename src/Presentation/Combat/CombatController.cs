using System.Collections.Generic;
using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.Combat
{
    public partial class CombatController : Control
    {
        private GameStateMachine _gsm = null!;
        private Label _timerLabel = null!;
        private HBoxContainer _playerBoard = null!;
        private HBoxContainer _enemyBoard = null!;
        private RichTextLabel _combatLog = null!;
        private RichTextLabel _pieceInfo = null!;
        private Button _continueBtn = null!;
        private Label _playerInfoLabel = null!;
        private Label _enemyInfoLabel = null!;

        private CombatResult? _result;
        private List<CombatEvent> _events = new();
        private float _playbackTime;
        private int _eventIndex;
        private float _combatDuration;
        private bool _combatFinished;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            _result = _gsm.LastCombatResult;
            _events = _result?.EventLog ?? new();
            _combatDuration = _result?.DurationSeconds ?? 30f;
            BuildUI();
            RenderBoards(_gsm.CurrentRun?.Board, _gsm.CurrentBot?.Board);
        }

        public override void _Process(double delta)
        {
            if (_combatFinished) return;

            _playbackTime += (float)delta;
            float displayTime = _combatDuration - _playbackTime;
            if (displayTime < 0) displayTime = 0;
            _timerLabel.Text = $"{displayTime:F1}s";

            while (_eventIndex < _events.Count && _events[_eventIndex].Timestamp <= _playbackTime)
            {
                var evt = _events[_eventIndex];
                _combatLog.AppendText(evt.ToLogString() + "\n");
                UpdateBoardVisuals(evt);
                _eventIndex++;
            }

            if (_playbackTime >= _combatDuration)
            {
                _combatFinished = true;
                _continueBtn.Disabled = false;
                _timerLabel.Text = "COMBAT OVER";
                RenderFinalState();
            }
        }

        private void BuildUI()
        {
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 8);
            AddChild(vbox);

            // Timer
            _timerLabel = new Label { Text = "30.0s", HorizontalAlignment = HorizontalAlignment.Center };
            _timerLabel.AddThemeFontSizeOverride("font_size", 32);
            vbox.AddChild(_timerLabel);

            var mainHBox = new HBoxContainer();
            mainHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
            mainHBox.AddThemeConstantOverride("separation", 12);
            vbox.AddChild(mainHBox);

            // Left panels
            var leftPanel = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
            _playerInfoLabel = new Label { Text = FormatPlayerInfo(), AutowrapMode = TextServer.AutowrapMode.Word };
            leftPanel.AddChild(_playerInfoLabel);
            var sep = new HSeparator();
            leftPanel.AddChild(sep);
            _enemyInfoLabel = new Label { Text = FormatEnemyInfo(), AutowrapMode = TextServer.AutowrapMode.Word };
            leftPanel.AddChild(_enemyInfoLabel);
            mainHBox.AddChild(leftPanel);

            // Center: boards + log
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _enemyBoard = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _enemyBoard.AddThemeConstantOverride("separation", 6);
            centerPanel.AddChild(_enemyBoard);

            _playerBoard = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _playerBoard.AddThemeConstantOverride("separation", 6);
            centerPanel.AddChild(_playerBoard);

            var logTitle = new Label { Text = "Combat Log" };
            centerPanel.AddChild(logTitle);

            var logScroll = new ScrollContainer();
            logScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            logScroll.CustomMinimumSize = new Vector2(0, 150);
            _combatLog = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = SizeFlags.ExpandFill };
            _combatLog.ScrollFollowing = true;
            logScroll.AddChild(_combatLog);
            centerPanel.AddChild(logScroll);

            mainHBox.AddChild(centerPanel);

            // Right: piece info + continue
            var rightPanel = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
            var infoTitle = new Label { Text = "Info" };
            infoTitle.AddThemeFontSizeOverride("font_size", 18);
            rightPanel.AddChild(infoTitle);
            _pieceInfo = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = SizeFlags.ExpandFill };
            rightPanel.AddChild(_pieceInfo);

            _continueBtn = new Button
            {
                Text = "Continue",
                CustomMinimumSize = new Vector2(0, 50),
                Disabled = true
            };
            _continueBtn.Pressed += OnContinue;
            rightPanel.AddChild(_continueBtn);

            mainHBox.AddChild(rightPanel);
        }

        private void RenderBoards(BoardState? player, BoardState? enemy)
        {
            RenderBoard(_playerBoard, player, "A");
            RenderBoard(_enemyBoard, enemy, "B");
        }

        private void RenderBoard(HBoxContainer container, BoardState? board, string prefix)
        {
            foreach (var c in container.GetChildren()) c.QueueFree();
            if (board == null) return;

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                int slot = i;
                var piece = board.GetSlot(i);
                var panel = new PanelContainer { CustomMinimumSize = new Vector2(110, 110) };

                var vbox = new VBoxContainer();
                var nameLabel = new Label
                {
                    Text = piece != null ? $"{prefix}{i + 1}: {piece.Definition.Type}" : $"{prefix}{i + 1}: Empty",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                vbox.AddChild(nameLabel);

                if (piece != null && !piece.IsDead)
                {
                    var hpBar = new ProgressBar
                    {
                        Value = 100,
                        CustomMinimumSize = new Vector2(0, 16),
                        ShowPercentage = false
                    };
                    vbox.AddChild(hpBar);

                    var statsLabel = new Label
                    {
                        Text = $"HP:{piece.CurrentHp} ATK:{piece.Atk}",
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    vbox.AddChild(statsLabel);

                    var cdBar = new ProgressBar
                    {
                        Value = 0,
                        CustomMinimumSize = new Vector2(0, 10),
                        ShowPercentage = false
                    };
                    vbox.AddChild(cdBar);
                }

                panel.AddChild(vbox);
                container.AddChild(panel);
            }
        }

        private void UpdateBoardVisuals(CombatEvent evt)
        {
            if (_result == null) return;

            if (evt.Type == CombatEventType.Damage || evt.Type == CombatEventType.PieceKilled ||
                evt.Type == CombatEventType.Heal || evt.Type == CombatEventType.BurnTick)
            {
                RenderBoards(_result.FinalPlayerBoard, _result.FinalEnemyBoard);
            }
        }

        private void RenderFinalState()
        {
            if (_result == null) return;
            RenderBoards(_result.FinalPlayerBoard, _result.FinalEnemyBoard);
        }

        private string FormatPlayerInfo()
        {
            var run = _gsm.CurrentRun;
            if (run == null) return "";
            return $"{run.PlayerName}\nScore: {run.TotalScore}\nLives: {run.Lives}\nPerks: {run.Perks.Count}";
        }

        private string FormatEnemyInfo()
        {
            var bot = _gsm.CurrentBot;
            if (bot == null) return "";
            return $"{bot.BotName}\nWins: {bot.Wins}\nLives: {bot.Lives}\nPerks: {bot.Perks.Count}";
        }

        private void OnContinue() => _gsm.OnCombatContinue();
    }
}
