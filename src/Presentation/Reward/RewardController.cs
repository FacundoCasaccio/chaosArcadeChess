using System.Collections.Generic;
using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Infrastructure.Balance;
using ChaosArcadeTower.Infrastructure.Content;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.Reward
{
    public partial class RewardController : Control
    {
        private GameStateMachine _gsm = null!;
        private List<PerkDefinition> _choices = new();
        private HBoxContainer _cardsContainer = null!;
        private PerkDefinition? _selected;
        private Button? _continueBtn;

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
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 20);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(vbox);

            var title = new Label
            {
                Text = "Select your reward",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 32);
            vbox.AddChild(title);

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
            vbox.AddChild(_cardsContainer);

            var btnBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            btnBar.AddThemeConstantOverride("separation", 20);

            var backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 45) };
            backBtn.Pressed += () => _gsm.TransitionTo(GameState.PostCombat);
            btnBar.AddChild(backBtn);

            _continueBtn = new Button
            {
                Text = "Continue",
                CustomMinimumSize = new Vector2(120, 45),
                Disabled = true
            };
            _continueBtn.Pressed += OnContinue;
            btnBar.AddChild(_continueBtn);

            vbox.AddChild(btnBar);
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

            string text = $"{perk.Type}\n{perk.Rarity}\n\n{perk.Name}\n\n{perk.Description}";
            btn.Text = text;
            btn.AddThemeColorOverride("font_color", color);

            return btn;
        }

        private void OnCardSelected(int index)
        {
            _selected = _choices[index];
            if (_continueBtn != null)
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

            string? targetPieceId = null;
            if (_selected.Target == PerkTarget.Piece && _gsm.CurrentRun != null)
            {
                var alive = _gsm.CurrentRun.Board.GetAlivePieces();
                if (alive.Count > 0)
                    targetPieceId = alive[0].Id;
            }

            _gsm.OnRewardChosen(_selected, targetPieceId);
        }
    }
}
