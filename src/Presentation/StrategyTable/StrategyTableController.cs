using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Presentation.GameFlow;
using ChaosArcadeTower.Presentation.Shared;
using ChaosArcadeTower.Simulation.Effects;

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
                var slotBtn = CreateDraggableSlot(slot, false);
                slotBtn.Pressed += () => OnSlotClicked(slot);
                slotBtn.MouseEntered += () => OnSlotHovered(slot);
                _boardContainer.AddChild(slotBtn);
            }
            panel.AddChild(_boardContainer);

            var reserveTitle = new Label { Text = "Reserve", HorizontalAlignment = HorizontalAlignment.Center };
            panel.AddChild(reserveTitle);

            _reserveContainer = new HBoxContainer();
            _reserveContainer.Alignment = BoxContainer.AlignmentMode.Center;
            _reserveContainer.AddThemeConstantOverride("separation", 8);
            panel.AddChild(_reserveContainer);
            RebuildReserveSlots();

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

        private DraggableSlot CreateDraggableSlot(int index, bool isReserve)
        {
            return new DraggableSlot
            {
                CustomMinimumSize = new Vector2(120, 130),
                Text = "Empty",
                ClipText = true,
                SlotCode = isReserve ? DraggableSlot.ReserveCode(index) : DraggableSlot.ActiveCode(index),
                OnSwapRequested = OnDragSwap
            };
        }

        private void OnDragSwap(int fromCode, int toCode)
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;

            bool fromRes = DraggableSlot.IsReserve(fromCode);
            bool toRes = DraggableSlot.IsReserve(toCode);
            int fromIdx = DraggableSlot.ToIndex(fromCode);
            int toIdx = DraggableSlot.ToIndex(toCode);

            if (!fromRes && !toRes)
                run.Board.SwapSlots(fromIdx, toIdx);
            else if (fromRes && !toRes)
                run.Board.SwapWithReserve(toIdx, fromIdx);
            else if (!fromRes && toRes)
                run.Board.SwapWithReserve(fromIdx, toIdx);

            _dragSourceSlot = -1;
            RefreshBoard();
        }

        private void RefreshBoard()
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;

            var registry = ServiceLocator.Get<PerkEffectRegistry>();
            var preview = PerkPreviewService.PreviewBoard(run.Board, run.Perks, registry);

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var btn = _boardContainer.GetChild<Button>(i);
                var piece = preview.GetSlot(i);
                btn.Text = piece != null ? FormatSlotText(piece, i) : $"Slot {i + 1}\n[Empty]";
            }

            RebuildReserveSlots();
        }

        private void RebuildReserveSlots()
        {
            foreach (var c in _reserveContainer.GetChildren()) c.QueueFree();

            var run = _gsm.CurrentRun;
            if (run == null) return;

            int maxRes = run.Board.MaxReserve;
            for (int i = 0; i < maxRes; i++)
            {
                int rIdx = i;
                var btn = CreateDraggableSlot(rIdx, true);
                btn.CustomMinimumSize = new Vector2(100, 100);
                btn.Text = i < run.Board.Reserve.Count
                    ? FormatSlotText(run.Board.Reserve[i], -1)
                    : "Reserve\n[Empty]";
                btn.Pressed += () => OnReserveClicked(rIdx);
                btn.MouseEntered += () => OnReserveHovered(rIdx);
                _reserveContainer.AddChild(btn);
            }
        }

        private static string FormatSlotText(PieceInstance piece, int slot)
        {
            string name = piece.Definition.Type.ToString();
            bool boosted = piece.BonusHp != 0 || piece.BonusAtk != 0 || piece.CooldownMultiplier < 0.999f;
            string marker = boosted ? "*" : "";
            return $"{name}{marker}\nHP:{piece.MaxHp} ATK:{piece.Atk}\nCD:{piece.EffectiveCooldown:F1} Val:{piece.Value}";
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
                OnSlotHovered(slotIndex);
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

        private void OnSlotHovered(int slot)
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;
            var registry = ServiceLocator.Get<PerkEffectRegistry>();
            var preview = PerkPreviewService.PreviewBoard(run.Board, run.Perks, registry);
            var piece = preview.GetSlot(slot);
            if (piece != null)
                _pieceInfo.Text = PieceInfoFormatter.Format(piece, slot, run.Perks);
        }

        private void OnReserveHovered(int reserveIndex)
        {
            var run = _gsm.CurrentRun;
            if (run == null) return;
            var registry = ServiceLocator.Get<PerkEffectRegistry>();
            var preview = PerkPreviewService.PreviewBoard(run.Board, run.Perks, registry);
            if (reserveIndex < preview.Reserve.Count)
                _pieceInfo.Text = PieceInfoFormatter.Format(preview.Reserve[reserveIndex], -1, run.Perks);
        }

        private void OnChallenge() => _gsm.StartCombat();
    }
}
