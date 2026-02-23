using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Presentation.GameFlow;
using ChaosArcadeTower.Presentation.Shared;

namespace ChaosArcadeTower.Presentation.StrategyTable
{
    public partial class StrategyTableController : Control
    {
        private GameStateMachine _gsm = null!;
        private HBoxContainer _boardContainer = null!;
        private HBoxContainer _reserveContainer = null!;
        private RichTextLabel _pieceInfo = null!;
        private RichTextLabel _perkInfo = null!;
        private Label _playerNameLabel = null!;
        private Label _livesLabel = null!;
        private Label _scoreLabel = null!;
        private Label _floorLabel = null!;
        private VBoxContainer _perkList = null!;
        private int _dragSourceSlot = -1;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            BuildUI();
            RefreshBoard();
            RefreshPlayerInfo();
            RefreshPerks();
        }

        private void BuildUI()
        {
            var mainHBox = new HBoxContainer();
            mainHBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mainHBox.AddThemeConstantOverride("separation", 12);
            AddChild(mainHBox);

            var leftPanel = BuildLeftPanel();
            mainHBox.AddChild(leftPanel);

            var centerPanel = BuildCenterPanel();
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            mainHBox.AddChild(centerPanel);

            var rightPanel = BuildRightPanel();
            mainHBox.AddChild(rightPanel);
        }

        private VBoxContainer BuildLeftPanel()
        {
            var panel = new VBoxContainer();
            panel.CustomMinimumSize = new Vector2(250, 0);
            panel.AddThemeConstantOverride("separation", 8);

            var infoPanel = new PanelContainer();
            var infoVBox = new VBoxContainer();
            _playerNameLabel = new Label { Text = "Player" };
            _playerNameLabel.AddThemeFontSizeOverride("font_size", 22);
            _livesLabel = new Label();
            _scoreLabel = new Label();
            _floorLabel = new Label();
            infoVBox.AddChild(_playerNameLabel);
            infoVBox.AddChild(_livesLabel);
            infoVBox.AddChild(_scoreLabel);
            infoVBox.AddChild(_floorLabel);
            infoPanel.AddChild(infoVBox);
            panel.AddChild(infoPanel);

            var perksTitle = new Label { Text = "Perks" };
            perksTitle.AddThemeFontSizeOverride("font_size", 18);
            panel.AddChild(perksTitle);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _perkList = new VBoxContainer();
            scroll.AddChild(_perkList);
            panel.AddChild(scroll);

            _perkInfo = new RichTextLabel();
            _perkInfo.CustomMinimumSize = new Vector2(0, 100);
            _perkInfo.BbcodeEnabled = true;
            _perkInfo.FitContent = true;
            panel.AddChild(_perkInfo);

            return panel;
        }

        private VBoxContainer BuildCenterPanel()
        {
            var panel = new VBoxContainer();
            panel.AddThemeConstantOverride("separation", 16);
            panel.Alignment = BoxContainer.AlignmentMode.Center;

            var boardTitle = new Label { Text = "Main Board", HorizontalAlignment = HorizontalAlignment.Center };
            boardTitle.AddThemeFontSizeOverride("font_size", 20);
            panel.AddChild(boardTitle);

            _boardContainer = new HBoxContainer();
            _boardContainer.Alignment = BoxContainer.AlignmentMode.Center;
            _boardContainer.AddThemeConstantOverride("separation", 8);
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                int slot = i;
                var slotBtn = CreateSlotButton(i);
                slotBtn.Pressed += () => OnSlotClicked(slot);
                _boardContainer.AddChild(slotBtn);
            }
            panel.AddChild(_boardContainer);

            var reserveTitle = new Label { Text = "Reserve", HorizontalAlignment = HorizontalAlignment.Center };
            panel.AddChild(reserveTitle);

            _reserveContainer = new HBoxContainer();
            _reserveContainer.Alignment = BoxContainer.AlignmentMode.Center;
            _reserveContainer.AddThemeConstantOverride("separation", 8);
            for (int i = 0; i < BoardState.DEFAULT_RESERVE_SLOTS; i++)
            {
                int rIdx = i;
                var btn = CreateSlotButton(-1);
                btn.CustomMinimumSize = new Vector2(100, 100);
                btn.Pressed += () => OnReserveClicked(rIdx);
                _reserveContainer.AddChild(btn);
            }
            panel.AddChild(_reserveContainer);

            var challengeBtn = new Button
            {
                Text = "Challenge",
                CustomMinimumSize = new Vector2(200, 60)
            };
            challengeBtn.AddThemeFontSizeOverride("font_size", 22);
            challengeBtn.Pressed += OnChallenge;
            var center = new CenterContainer();
            center.AddChild(challengeBtn);
            panel.AddChild(center);

            return panel;
        }

        private VBoxContainer BuildRightPanel()
        {
            var panel = new VBoxContainer();
            panel.CustomMinimumSize = new Vector2(280, 0);

            var title = new Label { Text = "Piece Info" };
            title.AddThemeFontSizeOverride("font_size", 18);
            panel.AddChild(title);

            _pieceInfo = new RichTextLabel();
            _pieceInfo.SizeFlagsVertical = SizeFlags.ExpandFill;
            _pieceInfo.BbcodeEnabled = true;
            _pieceInfo.FitContent = true;
            panel.AddChild(_pieceInfo);

            return panel;
        }

        private Button CreateSlotButton(int slotIndex)
        {
            return new Button
            {
                CustomMinimumSize = new Vector2(120, 130),
                Text = "Empty",
                ClipText = true
            };
        }

        private void RefreshBoard()
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var btn = _boardContainer.GetChild<Button>(i);
                var piece = run.Board.GetSlot(i);
                btn.Text = piece != null ? FormatSlotText(piece, i) : $"Slot {i + 1}\n[Empty]";
            }

            for (int i = 0; i < _reserveContainer.GetChildCount(); i++)
            {
                var btn = _reserveContainer.GetChild<Button>(i);
                btn.Text = i < run.Board.Reserve.Count
                    ? FormatSlotText(run.Board.Reserve[i], -1)
                    : "Reserve\n[Empty]";
            }
        }

        private string FormatSlotText(PieceInstance piece, int slot)
        {
            string name = piece.Definition.Type.ToString();
            return $"{name}\nHP:{piece.MaxHp} ATK:{piece.Atk}\nCD:{piece.Cooldown:F1} Val:{piece.Value}";
        }

        private void RefreshPlayerInfo()
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;
            _playerNameLabel.Text = run.PlayerName;
            _livesLabel.Text = $"Lives: {run.Lives}";
            _scoreLabel.Text = $"Score: {run.TotalScore}";
            _floorLabel.Text = $"Floor: {run.Floor}";
        }

        private void RefreshPerks()
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;

            foreach (var child in _perkList.GetChildren())
                child.QueueFree();

            foreach (var perk in run.Perks)
            {
                var label = new Label
                {
                    Text = $"[{perk.Definition.Rarity}] {perk.Definition.Name} x{perk.Stacks}",
                    AutowrapMode = TextServer.AutowrapMode.Word
                };
                _perkList.AddChild(label);
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;

            if (_dragSourceSlot >= 0)
            {
                run.Board.SwapSlots(_dragSourceSlot, slotIndex);
                _dragSourceSlot = -1;
                RefreshBoard();
                return;
            }

            var piece = run.Board.GetSlot(slotIndex);
            if (piece != null)
            {
                _dragSourceSlot = slotIndex;
                ShowPieceInfo(piece, slotIndex);
            }
        }

        private void OnReserveClicked(int reserveIndex)
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;

            if (_dragSourceSlot >= 0)
            {
                run.Board.SwapWithReserve(_dragSourceSlot, reserveIndex);
                _dragSourceSlot = -1;
                RefreshBoard();
            }
        }

        private void ShowPieceInfo(PieceInstance piece, int slot)
        {
            string info = $"[b]{piece.Definition.Type}[/b] (Slot {slot + 1})\n";
            info += $"HP: {piece.CurrentHp}/{piece.MaxHp}\n";
            info += $"ATK: {piece.Atk}\n";
            info += $"Cooldown: {piece.Cooldown:F2}s\n";
            info += $"Value: {piece.Value}\n";
            if (piece.Enchant.HasValue)
                info += $"Enchant: {piece.Enchant.Value}\n";
            if (piece.AppliedPerkIds.Count > 0)
                info += $"\nPerks applied: {piece.AppliedPerkIds.Count}";
            _pieceInfo.Text = info;
        }

        private void OnChallenge() => _gsm.StartCombat();
    }
}
