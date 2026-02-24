using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Infrastructure.Save;
using ChaosArcadeTower.Presentation.GameFlow;

namespace ChaosArcadeTower.Presentation.Options
{
    public partial class OptionsController : Control
    {
        private GameStateMachine _gsm = null!;
        private LineEdit _nameInput = null!;
        private HSlider _volumeSlider = null!;
        private CheckButton _fullscreenToggle = null!;
        private PlayerPrefs _prefs = null!;

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            _prefs = _gsm.GetSaveService().LoadPrefs();
            BuildUI();

            var mode = DisplayServer.WindowGetMode();
            bool isFullscreen = mode == DisplayServer.WindowMode.Fullscreen
                            || mode == DisplayServer.WindowMode.ExclusiveFullscreen;

            _fullscreenToggle.ButtonPressed = isFullscreen;
        }

        private void BuildUI()
        {
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 16);
            AddChild(vbox);

            var title = new Label { Text = "Options", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 36);
            vbox.AddChild(title);

            var settingsPanel = new VBoxContainer();
            settingsPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            settingsPanel.AddThemeConstantOverride("separation", 16);

            // Player Name
            var nameRow = new HBoxContainer();
            nameRow.AddChild(new Label { Text = "Player Name:", CustomMinimumSize = new Vector2(200, 0) });
            _nameInput = new LineEdit
            {
                Text = _prefs.PlayerName,
                CustomMinimumSize = new Vector2(250, 0),
                MaxLength = 20
            };
            nameRow.AddChild(_nameInput);
            settingsPanel.AddChild(nameRow);

            // Volume
            var volRow = new HBoxContainer();
            volRow.AddChild(new Label { Text = "Master Volume:", CustomMinimumSize = new Vector2(200, 0) });
            _volumeSlider = new HSlider
            {
                MinValue = 0, MaxValue = 100,
                Value = _prefs.MasterVolume * 100,
                CustomMinimumSize = new Vector2(250, 0)
            };
            volRow.AddChild(_volumeSlider);
            settingsPanel.AddChild(volRow);

            // Fullscreen
            var fsRow = new HBoxContainer();
            fsRow.AddChild(new Label { Text = "Fullscreen:", CustomMinimumSize = new Vector2(200, 0) });
            _fullscreenToggle = new CheckButton { ButtonPressed = _prefs.Fullscreen };
            fsRow.AddChild(_fullscreenToggle);
            settingsPanel.AddChild(fsRow);

            vbox.AddChild(settingsPanel);

            // Buttons
            var btnBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            btnBar.AddThemeConstantOverride("separation", 20);

            var saveBtn = new Button { Text = "Save", CustomMinimumSize = new Vector2(120, 45) };
            saveBtn.Pressed += OnSave;
            btnBar.AddChild(saveBtn);

            var backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 45) };
            backBtn.Pressed += () => _gsm.TransitionTo(GameState.MainMenu);
            btnBar.AddChild(backBtn);

            vbox.AddChild(btnBar);
        }

        private void OnSave()
        {
            _prefs.PlayerName = _nameInput.Text.Trim();
            if (_prefs.PlayerName.Length == 0)
                _prefs.PlayerName = "Player";
            _prefs.MasterVolume = (float)_volumeSlider.Value / 100f;
            _prefs.Fullscreen = _fullscreenToggle.ButtonPressed;

            _gsm.GetSaveService().SavePrefs(_prefs);

            if (_prefs.Fullscreen)
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            else
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        }
    }
}
