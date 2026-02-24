using System;
using System.Collections.Generic;
using Godot;
using ChaosArcadeTower.Core;
using ChaosArcadeTower.Core.Events;
using ChaosArcadeTower.Core.Random;
using ChaosArcadeTower.Domain.Board;
using ChaosArcadeTower.Domain.Combat;
using ChaosArcadeTower.Domain.Perks;
using ChaosArcadeTower.Domain.Pieces;
using ChaosArcadeTower.Domain.Run;
using ChaosArcadeTower.Infrastructure.Balance;
using ChaosArcadeTower.Infrastructure.Content;
using ChaosArcadeTower.Infrastructure.Save;
using ChaosArcadeTower.Simulation.Combat;
using ChaosArcadeTower.Simulation.Effects;
using ChaosArcadeTower.AI;

namespace ChaosArcadeTower.Presentation.GameFlow
{
    public partial class GameStateMachine : Node
    {
        private static readonly Dictionary<GameState, string> _scenePaths = new()
        {
            { GameState.MainMenu, "res://src/Presentation/Scenes/MainMenu.tscn" },
            { GameState.StrategyTable, "res://src/Presentation/Scenes/StrategyTable.tscn" },
            { GameState.MatchSetup, "res://src/Presentation/Scenes/MatchSetup.tscn" },
            { GameState.Combat, "res://src/Presentation/Scenes/Combat.tscn" },
            { GameState.PostCombat, "res://src/Presentation/Scenes/PostCombat.tscn" },
            { GameState.Reward, "res://src/Presentation/Scenes/Reward.tscn" },
            { GameState.Ranking, "res://src/Presentation/Scenes/Ranking.tscn" },
            { GameState.Options, "res://src/Presentation/Scenes/Options.tscn" },
        };

        public GameState CurrentState { get; private set; }
        public RunState? CurrentRun { get; private set; }
        public CombatResult? LastCombatResult { get; set; }
        public BotLoadout? CurrentBot { get; set; }
        public PieceInstance? PendingPieceGrant { get; set; }

        private ContentService _content = null!;
        private BalanceData _balance = null!;
        private CombatResolver _combatResolver = null!;
        private BotRunSimulator _botSimulator = null!;
        private DropTableService _dropTable = null!;
        private RewardRarityService _rarityService = null!;
        private DifficultyService _difficultyService = null!;
        private BoardPowerService _boardPowerService = null!;
        private ISaveService _saveService = null!;
        private PerkEffectRegistry _perkRegistry = null!;
        private IEventBus _eventBus = null!;
        private Node? _currentScene;

        public override void _Ready()
        {
            InitializeServices();
            TransitionTo(GameState.MainMenu);
        }

        private void InitializeServices()
        {
            _eventBus = new EventBus();
            _content = new ContentService();

            string balanceJson = LoadTextFile("res://Assets/Game/Data/Configs/Balance/balance_v0_1.json");
            _content.LoadBalance(balanceJson);

            string perksJson = LoadTextFile("res://Assets/Game/Data/Configs/Perks/perks_v0_1.json");
            _content.LoadPerks(perksJson);

            _balance = _content.Balance;
            _perkRegistry = new PerkEffectRegistry();
            _combatResolver = new CombatResolver(
                _balance.Globals.CombatDurationSeconds,
                _balance.Globals.TickSeconds,
                _balance.Globals.Score.EmptySlotAttackPoints,
                _perkRegistry);

            _dropTable = new DropTableService(_balance.Drops);
            _rarityService = new RewardRarityService(_balance.Rewards);
            _difficultyService = new DifficultyService(_balance.Difficulty);
            _boardPowerService = new BoardPowerService(_balance.Globals.BoardPowerWeights);
            _botSimulator = new BotRunSimulator(
                _content, _dropTable, _rarityService,
                _difficultyService, _boardPowerService, _balance);

            string savePath = OS.GetUserDataDir() + "/saves";
            _saveService = new JsonSaveService(savePath);

            var prefs = _saveService.LoadPrefs();

            if (prefs.Fullscreen)
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            else
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);

            ServiceLocator.Register<IEventBus>(_eventBus);
            ServiceLocator.Register(_content);
            ServiceLocator.Register(_combatResolver);
            ServiceLocator.Register(_botSimulator);
            ServiceLocator.Register(_dropTable);
            ServiceLocator.Register(_rarityService);
            ServiceLocator.Register(_difficultyService);
            ServiceLocator.Register(_boardPowerService);
            ServiceLocator.Register(_saveService);
            ServiceLocator.Register(_perkRegistry);
            ServiceLocator.Register(this);
        }

        public void TransitionTo(GameState state)
        {
            _currentScene?.QueueFree();
            _currentScene = null;

            CurrentState = state;

            if (_scenePaths.TryGetValue(state, out var path))
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene != null)
                {
                    _currentScene = scene.Instantiate();
                    AddChild(_currentScene);
                }
                else
                {
                    GD.PrintErr($"Failed to load scene: {path}");
                }
            }
        }

        public void StartNewRun()
        {
            var rng = new SeededRandomService((int)DateTime.UtcNow.Ticks);
            int seed = rng.NextInt(int.MaxValue);
            CurrentRun = new RunState
            {
                Seed = seed,
                Floor = 1,
                Lives = _balance.Globals.MaxLives,
                TotalScore = 0,
                PlayerName = _saveService.LoadPrefs().PlayerName
            };

            var pieceRng = new SeededRandomService(seed);
            var initialPieces = _dropTable.RollPieces(5, 1, pieceRng);
            for (int i = 0; i < initialPieces.Count; i++)
            {
                var def = _content.GetPieceDefinition(initialPieces[i]);
                CurrentRun.Board.SetSlot(i, new PieceInstance(def, $"p_{i}"));
            }

            TransitionTo(GameState.StrategyTable);
        }

        public void StartCombat()
        {
            if (CurrentRun == null) return;

            int botSeed = SeededRandomService.CombineSeed(CurrentRun.Seed, CurrentRun.Floor, 0);
            CurrentBot = _botSimulator.Generate(CurrentRun.Seed, CurrentRun.Floor);

            var posRng = new SeededRandomService(botSeed + 999);
            BotPositioner.Optimize(CurrentBot.Board, CurrentRun.Board, posRng);

            TransitionTo(GameState.MatchSetup);
        }

        public void RunCombat()
        {
            if (CurrentRun == null || CurrentBot == null) return;

            int combatSeed = SeededRandomService.CombineSeed(CurrentRun.Seed, CurrentRun.Floor, 42);
            LastCombatResult = _combatResolver.Resolve(
                CurrentRun.Board, CurrentBot.Board,
                CurrentRun.Perks, CurrentBot.Perks,
                combatSeed);

            TransitionTo(GameState.Combat);
        }

        public void OnCombatContinue()
        {
            TransitionTo(GameState.PostCombat);
        }

        public void OnPostCombatContinue()
        {
            if (CurrentRun == null || LastCombatResult == null) return;

            bool won = LastCombatResult.PlayerWon;
            if (won)
            {
                CurrentRun.AddScore(LastCombatResult.PlayerScore.Total);
                CurrentRun.Wins++;
            }
            else
            {
                CurrentRun.LoseLife();
            }

            if (CurrentRun.IsGameOver)
            {
                EndRun();
                return;
            }

            TransitionTo(GameState.Reward);
        }

        public void OnRewardChosen(PerkDefinition chosenPerk, string? targetPieceId, int? targetSlotIndex = null)
        {
            if (CurrentRun == null) return;

            string addPieceType = chosenPerk.GetStringParam("add_piece_type");
            if (!string.IsNullOrEmpty(addPieceType))
            {
                if (Enum.TryParse<PieceType>(addPieceType, true, out var pt))
                {
                    var def = _content.GetPieceDefinition(pt);
                    int pieceId = CurrentRun.Board.Reserve.Count + CurrentRun.Board.GetAllPieces().Count;
                    var piece = new PieceInstance(def, $"p_{pieceId}");
                    CurrentRun.Board.Reserve.Add(piece);
                }
                CurrentRun.AdvanceFloor();
                TransitionTo(GameState.StrategyTable);
                return;
            }

            string transformTo = chosenPerk.GetStringParam("transform_to");
            if (!string.IsNullOrEmpty(transformTo) && targetSlotIndex.HasValue)
            {
                if (Enum.TryParse<PieceType>(transformTo, true, out var newType))
                {
                    var oldPiece = CurrentRun.Board.GetSlot(targetSlotIndex.Value);
                    if (oldPiece != null)
                    {
                        var newDef = _content.GetPieceDefinition(newType);
                        var newPiece = new PieceInstance(newDef, oldPiece.Id)
                        {
                            BonusHp = oldPiece.BonusHp,
                            BonusAtk = oldPiece.BonusAtk,
                            CooldownMultiplier = oldPiece.CooldownMultiplier,
                            Enchant = oldPiece.Enchant
                        };
                        newPiece.AppliedPerkIds.AddRange(oldPiece.AppliedPerkIds);
                        newPiece.ApplyBonuses();
                        CurrentRun.Board.SetSlot(targetSlotIndex.Value, newPiece);
                    }
                }
                CurrentRun.AdvanceFloor();
                TransitionTo(GameState.StrategyTable);
                return;
            }

            var existing = CurrentRun.Perks.Find(p => p.Definition.Id == chosenPerk.Id);
            if (existing != null && existing.CanStack)
            {
                existing.Stacks++;
                if (targetPieceId != null) existing.TargetPieceId = targetPieceId;
                if (targetSlotIndex != null) existing.TargetSlotIndex = targetSlotIndex;
            }
            else
            {
                var instance = new PerkInstance(chosenPerk)
                {
                    TargetPieceId = targetPieceId,
                    TargetSlotIndex = targetSlotIndex
                };
                CurrentRun.Perks.Add(instance);
            }

            int reserveBonus = chosenPerk.GetIntParam("reserve_size_bonus");
            if (reserveBonus > 0)
                CurrentRun.Board.MaxReserve += reserveBonus;

            CurrentRun.AdvanceFloor();
            TransitionTo(GameState.StrategyTable);
        }

        public void OnPieceGrantReplacement(int reserveIndexToReplace)
        {
            if (CurrentRun == null || PendingPieceGrant == null) return;
            if (reserveIndexToReplace >= 0 && reserveIndexToReplace < CurrentRun.Board.Reserve.Count)
                CurrentRun.Board.Reserve[reserveIndexToReplace] = PendingPieceGrant;
            else
                CurrentRun.Board.Reserve.Add(PendingPieceGrant);
            PendingPieceGrant = null;
            CurrentRun.AdvanceFloor();
            TransitionTo(GameState.StrategyTable);
        }

        public void EndRun()
        {
            if (CurrentRun == null) return;

            _saveService.AddRankingEntry(new RankingEntry
            {
                PlayerName = CurrentRun.PlayerName,
                Score = CurrentRun.TotalScore,
                FloorReached = CurrentRun.Floor
            });

            TransitionTo(GameState.MainMenu);
        }

        public ContentService GetContent() => _content;
        public BalanceData GetBalance() => _balance;
        public ISaveService GetSaveService() => _saveService;
        public RewardRarityService GetRarityService() => _rarityService;

        private static string LoadTextFile(string godotPath)
        {
            if (FileAccess.FileExists(godotPath))
            {
                using var file = FileAccess.Open(godotPath, FileAccess.ModeFlags.Read);
                return file?.GetAsText() ?? "{}";
            }
            return "{}";
        }
    }
}
