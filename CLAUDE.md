# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity DOTS (Data-Oriented Technology Stack) prototype for a real-time strategy (RTS) game called "Warlords." The project demonstrates advanced Unity ECS (Entity Component System) patterns including flow field pathfinding, faction-based combat, and optimized performance systems.

**Unity Version**: Unity 6 (Unity 6000.0.15)
**Target Platforms**: Standalone Windows, with URP (Universal Render Pipeline)

## Key Technologies and Dependencies

- **Unity DOTS**: Core architecture using ECS (Entities 1.2.3)
- **Unity Physics**: Physics simulation and collision detection (1.2.4) 
- **Unity Mathematics**: High-performance math operations
- **Unity Entities Graphics**: Rendering for ECS entities (1.2.4)
- **Unity Input System**: Modern input handling (1.8.2)
- **Universal Render Pipeline**: Optimized rendering pipeline (17.0.3)
- **Burst Compiler**: High-performance compiled systems

## Architecture Overview

### Core ECS Pattern
The project follows strict DOTS principles with clear separation:
- **Authoring**: MonoBehaviour components that bake data into ECS components
- **Components**: Pure data structs implementing IComponentData
- **Systems**: Logic processors inheriting from ISystem with Burst compilation

### Faction System
Two-faction warfare system with `Faction.Friendly` and `Faction.Hostile` enums. Units target opposing factions automatically.

### Flow Field Pathfinding
Advanced pathfinding system using Dijkstra's algorithm:
- **FlowFieldSystem**: Calculates optimal movement directions for each faction
- **FlowFieldMovementSystem**: Applies flow field vectors to unit movement  
- Grid-based approach with dynamic cost calculation and obstacle avoidance
- Updates at 5Hz (0.2s intervals) for performance optimization

### Combat Systems
- **ShootingSystem**: Ranged combat with bullet spawning, accuracy spread, and cooldowns
- **MeleeAttackSystem**: Close-quarters combat mechanics
- **CollisionDetectionSystem**: Handles damage application on collision
- Health system with faction-based health tracking

### Performance Optimizations
- **Burst-compiled systems**: All systems use `[BurstCompile]` attribute
- **Batched processing**: Target finding uses frame-batched updates (15-frame cycles)
- **Spatial partitioning**: Chunk-based unit organization for efficient queries
- **Memory management**: Proper disposal of NativeArrays and blob assets

## Development Commands

### Building and Running
```bash
# Open project in Unity Editor
# Project builds through Unity's standard build system
# No command-line build setup currently configured
```

### Testing
```bash
# No automated test framework currently configured
# Testing done through Unity Play Mode
```

## Project Structure

```
Assets/
├── Scripts/
│   ├── Authoring/          # MonoBehaviour bakers for ECS conversion
│   ├── Data/               # Data containers and blob assets  
│   ├── Systems/            # ECS systems with game logic
│   │   └── CleanupSystems/ # Entity cleanup and lifetime management
│   ├── MonoBehaviours/     # Traditional Unity components  
│   └── UI/                 # User interface and camera controls
├── Prefabs/                # Unit and bullet prefabs
├── Materials/              # Visual materials and shaders
├── Scenes/                 # Game scenes
└── Settings/               # Rendering and quality settings
```

## Key Components and Systems

### Essential Components
- `Unit`: Core unit data with faction and targeting information
- `Health`: Health management with death state tracking
- `FlowFieldData`: Contains blob asset with pathfinding directions
- `TargetData`: Current target position and entity references
- `UnitMover`: Movement parameters (speed, rotation, stopping distance)

### Critical Systems Execution Order
1. `TargetFindingSystem` - Finds targets for units (frame-batched)
2. `FlowFieldSystem` - Calculates pathfinding fields (5Hz update)
3. `FlowFieldMovementSystem` - Applies movement from flow fields
4. `ShootingSystem` - Handles ranged combat
5. `MeleeAttackSystem` - Handles melee combat
6. `CollisionDetectionSystem` - Processes damage events

### Memory Management
- Always dispose NativeArrays after use
- Blob assets require manual disposal when replacing FlowFieldData
- Entity cleanup handled by dedicated cleanup systems

## Common Development Patterns

### Creating New Units
1. Create Authoring component inheriting from MonoBehaviour
2. Implement Baker class for ECS conversion
3. Add required IComponentData structs
4. Register in appropriate systems

### Adding New Systems  
1. Create partial struct implementing ISystem
2. Add [UpdateInGroup] and [UpdateAfter] attributes for ordering
3. Use [BurstCompile] for performance
4. Handle component lookups and queries properly

### Flow Field Integration
- Sample flow fields using `SampleFlowField()` in FlowFieldMovementSystem
- Units automatically follow gradients toward enemy positions
- Flow fields update every 0.2 seconds for all factions

## Performance Considerations

- Flow field grid size directly impacts performance (currently optimized for medium-scale battles)
- Burst compilation is essential - never remove [BurstCompile] attributes
- Use ComponentLookup for efficient random access to components
- Batch entity operations using EntityCommandBuffer
- Frame-batched updates prevent performance spikes with large unit counts

## Scene Setup

Primary scene: `Assets/Scenes/SampleScene.unity`
- Contains FlowFieldAuthoring singleton for pathfinding configuration
- GameAssets MonoBehaviour holds prefab references
- Camera setup with free-roam controls for testing

## Known Limitations

- No save/load system implemented
- Single-scene architecture (no scene management)
- No networking or multiplayer support
- UI system is minimal (health display only)
- No audio system integration