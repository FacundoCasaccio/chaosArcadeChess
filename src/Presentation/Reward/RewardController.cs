using System.Collections.Generic;
using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Infrastructure.Content;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.Reward
{
    public partial class RewardController : Control
    {
        private enum Step { ChoosingPerk, ChoosingTarget }

        private GameStateMachine _gsm = null!;
        private List<PerkDefinition> _choices = new();
        private Step _step = Step.ChoosingPerk;

        private VBoxContainer _root = null!;
        private Label _titleLabel = null!;
        private HBoxContainer _cardsContainer = null!;
        private HBoxContainer _btnBar = null!;
        private Button _continueBtn = null!;
        private Button _backBtn = null!;

        private VBoxContainer? _targetPanel;
        private HBoxContainer? _targetSlotsContainer;
        private Label? _targetPrompt;
        private Button? _confirmTargetBtn;
        private Button? _cancelTargetBtn;

        private PerkDefinition? _selected;
        private int _selectedCardIndex = -1;
        private int _selectedTargetSlot = -1;
        private string? _selectedTargetPieceId;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            GenerateChoices();
            BuildUI();
        }

        private void GenerateChoices()
        {
            var run = _gsm.CurrentRun;
            var result = _gsm.LastCombatResult;
            if (run == null || result == null) return;

            bool won = result.PlayerWon;
            var content = _gsm.GetContent();
            var rarityService = _gsm.GetRarityService();
            int count = rarityService.GetChoiceCount(won);

            int floor = run.Floor;
            var rng = new SeededRandomService(SeededRandomService.CombineSeed(run.Seed, floor, 777));

            for (int i = 0; i < count; i++)
            {
                var rarity = rarityService.RollRarity(floor, won, rng);
                var candidates = content.GetPerksByRarity(rarity);
                if (candidates.Count == 0)
                    candidates = content.GetPerksByRarity(Rarity.Common);
                if (candidates.Count > 0)
                    _choices.Add(candidates[rng.NextInt(candidates.Count)]);
            }
        }

        private void BuildUI()
        {
            _root = new VBoxContainer();
            _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _root.AddThemeConstantOverride("separation", 20);
            _root.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(_root);

            _titleLabel = new Label
            {
                Text = "Select your reward",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 32);
            _root.AddChild(_titleLabel);

            _cardsContainer = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            _cardsContainer.AddThemeConstantOverride("separation", 24);

            for (int i = 0; i < _choices.Count; i++)
            {
                int idx = i;
                var card = BuildCard(_choices[i]);
                card.Pressed += () => OnCardSelected(idx);
                _cardsContainer.AddChild(card);
            }
            _root.AddChild(_cardsContainer);

            _btnBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _btnBar.AddThemeConstantOverride("separation", 20);

            _backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 45) };
            _backBtn.Pressed += () => _gsm.TransitionTo(GameState.PostCombat);
            _btnBar.AddChild(_backBtn);

            _continueBtn = new Button
            {
                Text = "Continue",
                CustomMinimumSize = new Vector2(120, 45),
                Disabled = true
            };
            _continueBtn.Pressed += OnContinue;
            _btnBar.AddChild(_continueBtn);

            _root.AddChild(_btnBar);
        }

        private Button BuildCard(PerkDefinition perk)
        {
            var btn = new Button
            {
                CustomMinimumSize = new Vector2(220, 280),
                ClipText = false
            };

            var color = perk.Rarity switch
            {
                Rarity.Common => Colors.Gray,
                Rarity.Rare => Colors.DodgerBlue,
                Rarity.Epic => Colors.DarkOrchid,
                Rarity.Unique => Colors.Gold,
                _ => Colors.White
            };

            string targetHint = perk.NeedsTargetSelection ? " [target]" : "";
            string text = $"{perk.Type}\n{perk.Rarity}\n\n{perk.Name}{targetHint}\n\n{perk.Description}";
            btn.Text = text;
            btn.AddThemeColorOverride("font_color", color);

            return btn;
        }

        private void OnCardSelected(int index)
        {
            if (_step != Step.ChoosingPerk) return;

            _selected = _choices[index];
            _selectedCardIndex = index;
            _continueBtn.Disabled = false;

            for (int i = 0; i < _cardsContainer.GetChildCount(); i++)
            {
                var btn = _cardsContainer.GetChild<Button>(i);
                btn.Modulate = i == index ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.8f);
            }
        }

        private void OnContinue()
        {
            if (_selected == null) return;

            if (_selected.NeedsTargetSelection)
            {
                EnterTargetSelection();
                return;
            }

            _gsm.OnRewardChosen(_selected, null);
        }

        private void EnterTargetSelection()
        {
            _step = Step.ChoosingTarget;
            _selectedTargetSlot = -1;
            _selectedTargetPieceId = null;

            _cardsContainer.Visible = false;
            _btnBar.Visible = false;
            _titleLabel.Text = $"Select target for: {_selected!.Name}";

            _targetPanel = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            _targetPanel.AddThemeConstantOverride("separation", 16);

            _targetPrompt = new Label
            {
                Text = _selected.Description,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Word,
                CustomMinimumSize = new Vector2(500, 0)
            };
            _targetPrompt.AddThemeFontSizeOverride("font_size", 16);
            _targetPanel.AddChild(_targetPrompt);

            _targetSlotsContainer = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            _targetSlotsContainer.AddThemeConstantOverride("separation", 12);

            var board = _gsm.CurrentRun?.Board;
            if (board != null)
            {
                for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
                {
                    int slot = i;
                    var piece = board.GetSlot(i);
                    var slotBtn = BuildTargetSlotButton(piece, i);
                    slotBtn.Pressed += () => OnTargetSlotSelected(slot, piece);
                    _targetSlotsContainer.AddChild(slotBtn);
                }
            }
            _targetPanel.AddChild(_targetSlotsContainer);

            var reserveLabel = new Label
            {
                Text = "Reserve:",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            reserveLabel.AddThemeFontSizeOverride("font_size", 14);

            if (board != null && board.Reserve.Count > 0)
            {
                _targetPanel.AddChild(reserveLabel);
                var reserveRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
                reserveRow.AddThemeConstantOverride("separation", 12);
                for (int r = 0; r < board.Reserve.Count; r++)
                {
                    int rIdx = r;
                    var rPiece = board.Reserve[r];
                    var rBtn = BuildTargetSlotButton(rPiece, -1, isReserve: true, reserveIndex: r);
                    rBtn.Pressed += () => OnTargetReserveSelected(rIdx, rPiece);
                    reserveRow.AddChild(rBtn);
                }
                _targetPanel.AddChild(reserveRow);
            }

            var targetBtnBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            targetBtnBar.AddThemeConstantOverride("separation", 20);

            _cancelTargetBtn = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(120, 45) };
            _cancelTargetBtn.Pressed += OnCancelTarget;
            targetBtnBar.AddChild(_cancelTargetBtn);

            _confirmTargetBtn = new Button
            {
                Text = "Confirm",
                CustomMinimumSize = new Vector2(120, 45),
                Disabled = true
            };
            _confirmTargetBtn.Pressed += OnConfirmTarget;
            targetBtnBar.AddChild(_confirmTargetBtn);

            _targetPanel.AddChild(targetBtnBar);
            _root.AddChild(_targetPanel);
        }

        private Button BuildTargetSlotButton(PieceInstance? piece, int slotIndex,
            bool isReserve = false, int reserveIndex = -1)
        {
            bool valid = piece != null && !piece.IsDead;

            string label;
            if (piece == null)
                label = $"Slot {slotIndex + 1}\n[empty]";
            else if (piece.IsDead)
                label = $"Slot {slotIndex + 1}\n{piece.Definition.Type}\nDEAD";
            else if (isReserve)
                label = $"R{reserveIndex + 1}\n{piece.Definition.Type}\nHP:{piece.CurrentHp}/{piece.MaxHp}\nATK:{piece.Atk}";
            else
                label = $"Slot {slotIndex + 1}\n{piece.Definition.Type}\nHP:{piece.CurrentHp}/{piece.MaxHp}\nATK:{piece.Atk}";

            var btn = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(110, 100),
                Disabled = !valid
            };

            if (valid)
            {
                var normalStyle = new StyleBoxFlat
                {
                    BgColor = new Color(0.2f, 0.2f, 0.3f),
                    BorderWidthBottom = 2, BorderWidthTop = 2,
                    BorderWidthLeft = 2, BorderWidthRight = 2,
                    BorderColor = new Color(0.4f, 0.4f, 0.5f),
                    CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                    CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                    ContentMarginLeft = 4, ContentMarginRight = 4,
                    ContentMarginTop = 4, ContentMarginBottom = 4
                };
                btn.AddThemeStyleboxOverride("normal", normalStyle);
            }

            return btn;
        }

        private void OnTargetSlotSelected(int slot, PieceInstance? piece)
        {
            if (piece == null || piece.IsDead) return;

            _selectedTargetSlot = slot;
            _selectedTargetPieceId = piece.Id;

            HighlightTargetSelection();

            if (_confirmTargetBtn != null)
                _confirmTargetBtn.Disabled = false;
        }

        private void OnTargetReserveSelected(int reserveIndex, PieceInstance piece)
        {
            if (piece.IsDead) return;

            _selectedTargetSlot = -1;
            _selectedTargetPieceId = piece.Id;

            HighlightTargetSelection();

            if (_confirmTargetBtn != null)
                _confirmTargetBtn.Disabled = false;
        }

        private void HighlightTargetSelection()
        {
            if (_targetSlotsContainer == null) return;

            var board = _gsm.CurrentRun?.Board;
            if (board == null) return;

            for (int i = 0; i < _targetSlotsContainer.GetChildCount(); i++)
            {
                var btn = _targetSlotsContainer.GetChild<Button>(i);
                var piece = board.GetSlot(i);
                bool isSelected = piece != null && piece.Id == _selectedTargetPieceId;

                if (piece != null && !piece.IsDead)
                {
                    var style = new StyleBoxFlat
                    {
                        BgColor = isSelected ? new Color(0.15f, 0.4f, 0.15f) : new Color(0.2f, 0.2f, 0.3f),
                        BorderWidthBottom = 2, BorderWidthTop = 2,
                        BorderWidthLeft = 2, BorderWidthRight = 2,
                        BorderColor = isSelected ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.4f, 0.4f, 0.5f),
                        CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                        ContentMarginLeft = 4, ContentMarginRight = 4,
                        ContentMarginTop = 4, ContentMarginBottom = 4
                    };
                    btn.AddThemeStyleboxOverride("normal", style);
                }
            }

            HighlightReserveButtons();
        }

        private void HighlightReserveButtons()
        {
            if (_targetPanel == null) return;
            var board = _gsm.CurrentRun?.Board;
            if (board == null || board.Reserve.Count == 0) return;

            foreach (var child in _targetPanel.GetChildren())
            {
                if (child is HBoxContainer hbox && hbox != _targetSlotsContainer &&
                    hbox.GetChildCount() > 0 && hbox.GetChild(0) is Button)
                {
                    for (int r = 0; r < hbox.GetChildCount() && r < board.Reserve.Count; r++)
                    {
                        var btn = hbox.GetChild<Button>(r);
                        var piece = board.Reserve[r];
                        bool isSelected = piece.Id == _selectedTargetPieceId;

                        var style = new StyleBoxFlat
                        {
                            BgColor = isSelected ? new Color(0.15f, 0.4f, 0.15f) : new Color(0.2f, 0.2f, 0.3f),
                            BorderWidthBottom = 2, BorderWidthTop = 2,
                            BorderWidthLeft = 2, BorderWidthRight = 2,
                            BorderColor = isSelected ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.4f, 0.4f, 0.5f),
                            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                            ContentMarginLeft = 4, ContentMarginRight = 4,
                            ContentMarginTop = 4, ContentMarginBottom = 4
                        };
                        btn.AddThemeStyleboxOverride("normal", style);
                    }
                    break;
                }
            }
        }

        private void OnConfirmTarget()
        {
            if (_selected == null || _selectedTargetPieceId == null) return;

            int? slotIdx = _selectedTargetSlot >= 0 ? _selectedTargetSlot : null;
            _gsm.OnRewardChosen(_selected, _selectedTargetPieceId, slotIdx);
        }

        private void OnCancelTarget()
        {
            _step = Step.ChoosingPerk;
            _selectedTargetSlot = -1;
            _selectedTargetPieceId = null;

            if (_targetPanel != null)
            {
                _targetPanel.QueueFree();
                _targetPanel = null;
            }

            _titleLabel.Text = "Select your reward";
            _cardsContainer.Visible = true;
            _btnBar.Visible = true;
        }

        private string FormatPlayerInfo()
        {
            var run = _gsm.CurrentRun;
            if (run == null) return "";
            return $"{run.PlayerName}\nScore: {run.TotalScore}\nLives: {run.Lives}\nPerks: {run.Perks.Count}";
        }
    }
}
