using System.Collections.Generic;
using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Presentation.GameFlow;
using CombatSide = ChaosArcadeTower.Domain.Combat.Side;

namespace ChaosArcadeTower.Presentation.Combat
{
    public partial class CombatController : Control
    {
        private const int MAX_LOG_LINES = 300;
        private const float CD_UPDATE_INTERVAL = 0.1f;

        private GameStateMachine _gsm = null!;
        private Label _timerLabel = null!;
        private HBoxContainer _playerBoardContainer = null!;
        private HBoxContainer _enemyBoardContainer = null!;
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
        private int _logLineCount;
        private float _cdUpdateTimer;

        private BoardState _pbPlayer = new();
        private BoardState _pbEnemy = new();

        private readonly float[] _cdStartPlayer = new float[BoardState.ACTIVE_SLOTS];
        private readonly float[] _cdLenPlayer = new float[BoardState.ACTIVE_SLOTS];
        private readonly float[] _cdStartEnemy = new float[BoardState.ACTIVE_SLOTS];
        private readonly float[] _cdLenEnemy = new float[BoardState.ACTIVE_SLOTS];

        private readonly ProgressBar?[] _cdBarsPlayer = new ProgressBar?[BoardState.ACTIVE_SLOTS];
        private readonly ProgressBar?[] _cdBarsEnemy = new ProgressBar?[BoardState.ACTIVE_SLOTS];

        public override void _Ready()
        {
            _gsm = ServiceLocator.Get<GameStateMachine>();
            _result = _gsm.LastCombatResult;
            _events = _result?.EventLog ?? new();
            _combatDuration = _result?.DurationSeconds ?? 15f;

            _pbPlayer = _result?.InitialPlayerBoard?.DeepClone() ?? new BoardState();
            _pbEnemy = _result?.InitialEnemyBoard?.DeepClone() ?? new BoardState();

            float tickSec = _gsm.GetBalance().Globals.TickSeconds;
            InitCooldownTracking(_pbPlayer, _cdStartPlayer, _cdLenPlayer, tickSec);
            InitCooldownTracking(_pbEnemy, _cdStartEnemy, _cdLenEnemy, tickSec);

            BuildUI();
            RenderBoards();
            UpdateCooldownBars();
        }

        public override void _Process(double delta)
        {
            if (_combatFinished) return;

            _playbackTime += (float)delta;
            float displayTime = _combatDuration - _playbackTime;
            if (displayTime < 0) displayTime = 0;
            _timerLabel.Text = $"{displayTime:F1}s";

            bool boardDirty = false;
            while (_eventIndex < _events.Count && _events[_eventIndex].Timestamp <= _playbackTime)
            {
                var evt = _events[_eventIndex];
                AppendCombatEvent(evt);
                ApplyEventToPlayback(evt);
                UpdateCooldownTracking(evt);
                boardDirty = true;
                _eventIndex++;
            }

            if (boardDirty)
                RenderBoards();

            _cdUpdateTimer += (float)delta;
            if (_cdUpdateTimer >= CD_UPDATE_INTERVAL)
            {
                _cdUpdateTimer -= CD_UPDATE_INTERVAL;
                UpdateCooldownBars();
            }

            if (_playbackTime >= _combatDuration)
            {
                _combatFinished = true;
                _continueBtn.Disabled = false;
                _timerLabel.Text = "COMBAT OVER";
            }
        }

        private static void InitCooldownTracking(BoardState board, float[] starts, float[] lengths, float tickSec)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                starts[i] = 0f;
                var piece = board.GetSlot(i);
                lengths[i] = piece != null ? piece.EffectiveCooldown + i * tickSec : 1f;
            }
        }

        private void UpdateCooldownTracking(CombatEvent evt)
        {
            bool isAction = evt.Type == CombatEventType.Damage
                         || evt.Type == CombatEventType.EmptySlotHit
                         || evt.Type == CombatEventType.Heal;
            if (!isAction) return;

            int slot = evt.SourceSlot;
            if (slot < 0 || slot >= BoardState.ACTIVE_SLOTS) return;

            var starts = evt.SourceSide == CombatSide.Player ? _cdStartPlayer : _cdStartEnemy;
            var lengths = evt.SourceSide == CombatSide.Player ? _cdLenPlayer : _cdLenEnemy;

            if (evt.Timestamp <= starts[slot]) return;

            starts[slot] = evt.Timestamp;
            var piece = GetPlaybackPiece(evt.SourceSide, slot);
            if (piece != null)
                lengths[slot] = piece.EffectiveCooldown;
        }

        private void UpdateCooldownBars()
        {
            UpdateCooldownBarsForSide(_cdBarsPlayer, _cdStartPlayer, _cdLenPlayer, _pbPlayer);
            UpdateCooldownBarsForSide(_cdBarsEnemy, _cdStartEnemy, _cdLenEnemy, _pbEnemy);
        }

        private void UpdateCooldownBarsForSide(
            ProgressBar?[] bars, float[] starts, float[] lengths, BoardState board)
        {
            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                var bar = bars[i];
                if (bar == null || !IsInstanceValid(bar)) continue;

                var piece = board.GetSlot(i);
                if (piece == null || piece.IsDead)
                {
                    bar.Value = 0;
                    continue;
                }

                float fill = lengths[i] > 0f
                    ? (_playbackTime - starts[i]) / lengths[i]
                    : 0f;
                bar.Value = Mathf.Clamp(fill, 0f, 1f) * 100f;
            }
        }

        private void BuildUI()
        {
            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 8);
            AddChild(vbox);

            _timerLabel = new Label
            {
                Text = $"{_combatDuration:F1}s",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _timerLabel.AddThemeFontSizeOverride("font_size", 32);
            vbox.AddChild(_timerLabel);

            var mainHBox = new HBoxContainer();
            mainHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
            mainHBox.AddThemeConstantOverride("separation", 12);
            vbox.AddChild(mainHBox);

            var leftPanel = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
            _playerInfoLabel = new Label { Text = FormatPlayerInfo(), AutowrapMode = TextServer.AutowrapMode.Word };
            leftPanel.AddChild(_playerInfoLabel);
            leftPanel.AddChild(new HSeparator());
            _enemyInfoLabel = new Label { Text = FormatEnemyInfo(), AutowrapMode = TextServer.AutowrapMode.Word };
            leftPanel.AddChild(_enemyInfoLabel);
            mainHBox.AddChild(leftPanel);

            var centerPanel = new VBoxContainer();
            centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _enemyBoardContainer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _enemyBoardContainer.AddThemeConstantOverride("separation", 6);
            centerPanel.AddChild(_enemyBoardContainer);

            _playerBoardContainer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _playerBoardContainer.AddThemeConstantOverride("separation", 6);
            centerPanel.AddChild(_playerBoardContainer);

            var logTitle = new Label { Text = "Combat Log" };
            logTitle.AddThemeFontSizeOverride("font_size", 14);
            centerPanel.AddChild(logTitle);

            _combatLog = new RichTextLabel
            {
                BbcodeEnabled = true,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                ScrollFollowing = true,
                CustomMinimumSize = new Vector2(0, 180)
            };
            _combatLog.AddThemeFontSizeOverride("normal_font_size", 12);
            centerPanel.AddChild(_combatLog);

            mainHBox.AddChild(centerPanel);

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

        private void RenderBoards()
        {
            RenderBoard(_playerBoardContainer, _pbPlayer, "A", _cdBarsPlayer);
            RenderBoard(_enemyBoardContainer, _pbEnemy, "B", _cdBarsEnemy);
        }

        private void RenderBoard(HBoxContainer container, BoardState board, string prefix, ProgressBar?[] cdBars)
        {
            foreach (var c in container.GetChildren()) c.QueueFree();

            for (int i = 0; i < BoardState.ACTIVE_SLOTS; i++)
            {
                cdBars[i] = null;
                var piece = board.GetSlot(i);
                var panel = new PanelContainer { CustomMinimumSize = new Vector2(110, 120) };
                var vbox = new VBoxContainer();

                var nameLabel = new Label
                {
                    Text = piece != null ? $"{prefix}{i + 1}: {piece.Definition.Type}" : $"{prefix}{i + 1}: Empty",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                vbox.AddChild(nameLabel);

                if (piece != null && !piece.IsDead)
                {
                    float hpPct = piece.MaxHp > 0 ? (float)piece.CurrentHp / piece.MaxHp * 100f : 0f;
                    var hpBar = new ProgressBar
                    {
                        Value = hpPct,
                        CustomMinimumSize = new Vector2(0, 14),
                        ShowPercentage = false
                    };
                    vbox.AddChild(hpBar);

                    var statsLabel = new Label
                    {
                        Text = $"HP:{piece.CurrentHp}/{piece.MaxHp} ATK:{piece.Atk}",
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    statsLabel.AddThemeFontSizeOverride("font_size", 11);
                    vbox.AddChild(statsLabel);

                    var cdLabel = new Label
                    {
                        Text = "CD",
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    cdLabel.AddThemeFontSizeOverride("font_size", 10);
                    cdLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));
                    vbox.AddChild(cdLabel);

                    var cdBar = new ProgressBar
                    {
                        CustomMinimumSize = new Vector2(0, 10),
                        ShowPercentage = false,
                        MinValue = 0,
                        MaxValue = 100
                    };
                    var cdFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.55f, 0.9f) };
                    var cdBg = new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.25f) };
                    cdBar.AddThemeStyleboxOverride("fill", cdFill);
                    cdBar.AddThemeStyleboxOverride("background", cdBg);
                    vbox.AddChild(cdBar);
                    cdBars[i] = cdBar;
                }
                else if (piece != null && piece.IsDead)
                {
                    var deadLabel = new Label
                    {
                        Text = "DEAD",
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    deadLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.2f, 0.2f));
                    vbox.AddChild(deadLabel);
                }

                panel.AddChild(vbox);
                container.AddChild(panel);
            }
        }

        private void ApplyEventToPlayback(CombatEvent evt)
        {
            PieceInstance? piece;
            switch (evt.Type)
            {
                case CombatEventType.Damage:
                case CombatEventType.BurnTick:
                    piece = GetPlaybackPiece(evt.TargetSide, evt.TargetSlot);
                    if (piece != null)
                        piece.CurrentHp = evt.TargetHpAfter;
                    break;
                case CombatEventType.Heal:
                    piece = GetPlaybackPiece(evt.TargetSide, evt.TargetSlot);
                    if (piece != null)
                        piece.CurrentHp = evt.TargetHpAfter;
                    break;
                case CombatEventType.PieceKilled:
                    piece = GetPlaybackPiece(evt.TargetSide, evt.TargetSlot);
                    if (piece != null)
                        piece.CurrentHp = 0;
                    break;
            }
        }

        private PieceInstance? GetPlaybackPiece(CombatSide side, int slot)
        {
            var board = side == CombatSide.Player ? _pbPlayer : _pbEnemy;
            return board.GetSlot(slot);
        }

        private void AppendCombatEvent(CombatEvent evt)
        {
            string color = evt.Type switch
            {
                CombatEventType.Damage => "#ffffff",
                CombatEventType.PieceKilled => "#ff4444",
                CombatEventType.Heal => "#44ff44",
                CombatEventType.BurnTick => "#ff8844",
                CombatEventType.EmptySlotHit => "#888888",
                CombatEventType.StatusApplied => "#ffff44",
                CombatEventType.PerkTriggered => "#44ccff",
                _ => "#cccccc"
            };

            _combatLog.AppendText($"[color={color}]{evt.ToLogString()}[/color]\n");
            _logLineCount++;

            while (_logLineCount > MAX_LOG_LINES)
            {
                _combatLog.RemoveParagraph(0);
                _logLineCount--;
            }
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
