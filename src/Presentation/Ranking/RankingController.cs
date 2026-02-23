using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Infrastructure.Save;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.Ranking
{
    public partial class RankingController : Control
    {
        private GameStateMachine _gsm = null!;
        private VBoxContainer _listContainer = null!;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            BuildUI();
        }

        private void BuildUI()
        {
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 12);
            AddChild(vbox);

            var title = new Label { Text = "Ranking", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 36);
            vbox.AddChild(title);

            // Header row
            var header = new HBoxContainer();
            header.AddChild(MakeHeaderLabel("#", 50));
            header.AddChild(MakeHeaderLabel("Name", 200));
            header.AddChild(MakeHeaderLabel("Score", 100));
            header.AddChild(MakeHeaderLabel("Floor", 80));
            header.AddChild(MakeHeaderLabel("Date", 200));
            vbox.AddChild(header);

            vbox.AddChild(new HSeparator());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _listContainer = new VBoxContainer();
            _listContainer.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_listContainer);
            vbox.AddChild(scroll);

            PopulateList();

            // Buttons
            var btnBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            btnBar.AddThemeConstantOverride("separation", 16);

            var backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 45) };
            backBtn.Pressed += () => _gsm.TransitionTo(GameState.MainMenu);
            btnBar.AddChild(backBtn);

            var resetBtn = new Button { Text = "Reset Ranking", CustomMinimumSize = new Vector2(150, 45) };
            resetBtn.Pressed += OnReset;
            btnBar.AddChild(resetBtn);

            vbox.AddChild(btnBar);
        }

        private void PopulateList()
        {
            foreach (var c in _listContainer.GetChildren())
                c.QueueFree();

            var save = _gsm.GetSaveService();
            var data = save.LoadRanking();

            for (int i = 0; i < data.Entries.Count; i++)
            {
                var entry = data.Entries[i];
                var row = new HBoxContainer();
                row.AddChild(MakeLabel($"{i + 1}", 50));
                row.AddChild(MakeLabel(entry.PlayerName, 200));
                row.AddChild(MakeLabel(entry.Score.ToString(), 100));
                row.AddChild(MakeLabel(entry.FloorReached.ToString(), 80));
                row.AddChild(MakeLabel(entry.DateUtc.Length > 10 ? entry.DateUtc[..10] : entry.DateUtc, 200));
                _listContainer.AddChild(row);
            }

            if (data.Entries.Count == 0)
            {
                _listContainer.AddChild(new Label
                {
                    Text = "No scores yet. Play a run!",
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
        }

        private Label MakeHeaderLabel(string text, float minWidth)
        {
            var lbl = new Label
            {
                Text = text,
                CustomMinimumSize = new Vector2(minWidth, 0)
            };
            lbl.AddThemeFontSizeOverride("font_size", 18);
            return lbl;
        }

        private Label MakeLabel(string text, float minWidth)
        {
            return new Label
            {
                Text = text,
                CustomMinimumSize = new Vector2(minWidth, 0)
            };
        }

        private void OnReset()
        {
            _gsm.GetSaveService().ResetRanking();
            PopulateList();
        }
    }
}
