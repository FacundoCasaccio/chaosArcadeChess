# Chaos Arcade Tower

Roguelike + Autobattler + Chess arcade game. Climb an infinite tower, fight auto-battling chess pieces, collect perks, build synergies, and chase high scores.

## Requirements

- **Godot Engine 4.3+** with .NET support (C#)
- **.NET SDK 6.0+**
- Windows for final build export (dev works on macOS/Linux too)

## Setup

1. Install [Godot 4.3 .NET](https://godotengine.org/download)
2. Clone this repo
3. Open `project.godot` in Godot
4. Godot will generate the `.godot/` directory and build the C# solution
5. Press F5 to run

## Project Structure

```
src/Core/           - Interfaces, RNG, event bus, service locator
src/Domain/         - Pure C# models (Piece, Perk, Board, Run, Combat)
src/Simulation/     - Deterministic combat engine, effects, scoring
src/AI/             - Bot run simulator, positioning heuristics
src/Presentation/   - Godot scenes, UI controllers, game flow
src/Infrastructure/ - Config loading, save/ranking, balance services
Assets/Game/Data/   - JSON/YAML configs (balance, perks)
docs/               - GDD, TDD, Competitive Analysis
```

## Architecture

- **Domain layer**: Zero Godot dependencies, pure C# models
- **Simulation**: Deterministic tick-based combat (same seed = same result)
- **Data-driven**: All piece stats, perks, and drop tables in JSON configs
- **Seeded RNG**: Every random decision flows through SeededRandomService

## Game Flow

MainMenu -> StrategyTable -> MatchSetup (positioning) -> Combat (30s auto) -> PostCombat (score) -> Reward (pick 1 of 3) -> next floor

## Windows Build

1. In Godot: Project > Export > Windows Desktop
2. Click "Export Project"
3. Output: `export/ChaosArcadeTower.exe`
# chaosArcadeChess
