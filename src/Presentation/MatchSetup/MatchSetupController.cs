using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.MatchSetup
{
    public partial class MatchSetupController : Control
    {
        private GameStateMachine _gsm = null!;
        private Label _timerLabel = null!;
        private Label _floorLabel = null!;
        private HBoxContainer _playerBoard = null!;
        private HBoxContainer _enemyBoard = null!;
        private RichTextLabel _pieceInfo = null!;
        private Label _playerInfoLabel = null!;
        private Label _enemyInfoLabel = null!;
        private float _positioningTime = 7f;
        private float _elapsed;
        private int _swapSource = -1;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            BuildUI();
            RefreshBoards();
        }

        public override void _Process(double delta)
        {
            _elapsed += (float)delta;
            float remaining = _positioningTime - _elapsed;
            if (remaining < 0) remaining = 0;
            _timerLabel.Text = $"Position your pieces! {remaining:F0}s";

            if (_elapsed >= _positioningTime)
                _gsm.RunCombat();
        }

        private void BuildUI()
        {
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 12);
            AddChild(vbox);

            var topBar = new HBoxContainer();
            topBar.Alignment = BoxContainer.AlignmentMode.Center;
            _floorLabel = new Label { Text = $"Floor {_gsm.CurrentRun?.Floor ?? 1}" };
            _floorLabel.AddThemeFontSizeOverride("font_size", 28);
            topBar.AddChild(_floorLabel);
            vbox.AddChild(topBar);

            var mainHBox = new HBoxContainer();
            mainHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
            mainHBox.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(mainHBox);

            // Left - enemy info
            var leftPanel = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) };
            _enemyInfoLabel = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
            leftPanel.AddChild(_enemyInfoLabel);
            mainHBox.AddChild(leftPanel);

            // Center - boards
            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerPanel.Alignment = BoxContainer.AlignmentMode.Center;

            var enemyLabel = new Label { Text = "Opponent", HorizontalAlignment = HorizontalAlignment.Center };
            centerPanel.AddChild(enemyLabel);

            _enemyBoard = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _enemyBoard.AddThemeConstantOverride("separation", 6);
            centerPanel.AddChild(_enemyBoard);

            var vsLabel = new Label { Text = "VS", HorizontalAlignment = HorizontalAlignment.Center };
            vsLabel.AddThemeFontSizeOverride("font_size", 30);
            centerPanel.AddChild(vsLabel);

            _playerBoard = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _playerBoard.AddThemeConstantOverride("separation", 6);
            centerPanel.AddChild(_playerBoard);

            var youLabel = new Label { Text = "You", HorizontalAlignment = HorizontalAlignment.Center };
            centerPanel.AddChild(youLabel);

            mainHBox.AddChild(centerPanel);

            // Right - piece info
            var rightPanel = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
            var infoTitle = new Label { Text = "Piece Info" };
            infoTitle.AddThemeFontSizeOverride("font_size", 18);
            rightPanel.AddChild(infoTitle);
            _pieceInfo = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = SizeFlags.ExpandFill };
            rightPanel.AddChild(_pieceInfo);
            mainHBox.AddChild(rightPanel);

            // Timer bar
            _timerLabel = new Label
            {
                Text = "Position your pieces!",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _timerLabel.AddThemeFontSizeOverride("font_size", 22);
            vbox.AddChild(_timerLabel);

            // Player info
            UpdateInfoPanels();
        }

        private void RefreshBoards()
        {
            CreateBoardSlots(_playerBoard, true);
            CreateBoardSlots(_enemyBoard, false);
        }

        private void CreateBoardSlots(HBoxContainer container, bool isPlayer)
        {
            foreach (var child in container.GetChildren())
                child.QueueFree();

            var board = isPlayer ? _gsm.CurrentRun?.Board : _gsm.CurrentBot?.Board;
            if (board == null) return;

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                int slot = i;
                var piece = board.GetSlot(i);
                var btn = new Button
                {
                    CustomMinimumSize = new Vector2(110, 110),
                    Text = piece != null ? $"{piece.Definition.Type}\nHP:{piece.MaxHp}\nATK:{piece.Atk}" : "[Empty]"
                };

                if (isPlayer)
                {
                    btn.Pressed += () => OnPlayerSlotClicked(slot);
                }
                else
                {
                    btn.Pressed += () =>
                    {
                        if (piece != null)
                            _pieceInfo.Text = $"[b]{piece.Definition.Type}[/b]\nHP: {piece.MaxHp}\nATK: {piece.Atk}\nCD: {piece.Cooldown:F2}s\nValue: {piece.Value}";
                    };
                }
                container.AddChild(btn);
            }
        }

        private void OnPlayerSlotClicked(int slot)
        {
            if (_swapSource >= 0)
            {
                _gsm.CurrentRun?.Board.SwapSlots(_swapSource, slot);
                _swapSource = -1;
                RefreshBoards();
            }
            else
            {
                _swapSource = slot;
                var piece = _gsm.CurrentRun?.Board.GetSlot(slot);
                if (piece != null)
                    _pieceInfo.Text = $"[b]{piece.Definition.Type}[/b] (Slot {slot + 1})\nHP: {piece.MaxHp}\nATK: {piece.Atk}\nCD: {piece.Cooldown:F2}s\nClick another slot to swap.";
            }
        }

        private void UpdateInfoPanels()
        {
            if (_gsm.CurrentBot != null)
            {
                var bot = _gsm.CurrentBot;
                _enemyInfoLabel.Text = $"{bot.BotName}\nWins: {bot.Wins}\nLives: {bot.Lives}\nPerks: {bot.Perks.Count}";
            }

            if (_gsm.CurrentRun != null)
            {
                _playerInfoLabel = _playerInfoLabel ?? new Label();
            }
        }
    }
}
