[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)

# G33kColony

**G33kColony** is a C# ant colony simulation exploring emergent behaviour through pheromone-driven swarm intelligence.

The goal is simple: simulate a bunch of very dumb agents, and watch surprisingly smart behaviour emerge.

---

## The idea

Ants are individually simple, but collectively powerful. By following a few rules:

- wander
- follow trails
- reinforce useful paths
- forget bad ones

…they can discover efficient routes between their nest and food sources.

G33kColony aims to model this using a clean, tweakable simulation that makes the behaviour easy to observe and experiment with.

---

## Simulation overview

The world is a continuous 2D space.

The world contains:
- finite circular food blobs, grouped into small clusters around each food source vicinity
- circular pheromone blobs:
  - **home pheromone**
  - **food pheromone**

Each ant has:
- an absolute position
- a heading
- a small body radius used to avoid overlapping other ants
- a state:
  - `Searching`
  - `Returning`

Ants spawn from the nest. If another ant is within the spawn radius, that ant waits and tries again on a later tick.

---

## Algorithm (Dual pheromone system)

### Searching ants

- Leave the nest and perform a biased random walk:
  - maintain heading
  - apply small random angle turns that accumulate over time
- New ants spawned at the nest face toward nearby **food pheromone** when present
- Every few steps, deposit a **home pheromone** blob at the current position
- Look ahead using three sample circles:
  - left
  - straight ahead
  - right
- If a food blob is detected in those circles, turn directly toward it
- If **food pheromone** is detected, bias movement toward the strongest sample
  - random turns still happen, but become less likely when the food scent is stronger
- If food is found:
  - consume one unit from the food blob
  - pick up food
  - switch to `Returning`
  - refresh ant life

---

### Returning ants

- Move toward the nest by sampling **home pheromone** blobs with the same left/straight/right look-ahead
- If the nest is detected, turn directly toward it
- If **home pheromone** is detected, random turning is suppressed while following it
- Every few steps, deposit a **food pheromone** blob
- If the nest is reached:
  - drop food
  - respawn fresh at the nest as `Searching`

---

### Pheromone behaviour

- Both pheromones:
  - **evaporate over time**
- Strong, frequently used paths become reinforced
- Weak or unused paths fade away

---

## Movement model

At each simulation tick:

1. Each ant:
   - samples the relevant pheromone layer in three circles ahead
   - selects a direction based on:
     - pheromone strength
     - current heading
     - small random angle changes
   - moves forward one step
   - tries a nearby heading instead if that step would overlap another ant
   - periodically deposits pheromone depending on state
   - ages, unless it has just found food

2. The world:
   - applies pheromone evaporation

This loop produces emergent pathfinding without any explicit pathfinding algorithm.

---

## Rendering

The simulation renders continuous positions to a bitmap:

- pheromone blobs visualised as intensity/colour overlays
- finite food blobs rendered by remaining amount
- ants rendered as moving points

Potential overlays:
- food pheromone
- home pheromone
- ant look-ahead sensor areas and body radius
- ant paths / density

Current first pass:
- fixed 640x480 world space
- bitmap renderer scaled uniformly to the window
- menu toggles for home pheromones, food pheromones, and sensor overlay visibility
- top-right controls for seed, restart, decay, trail strength, turn chance, turn angle, simulation speed, ant count, ant life, food source count, and settings reset
- UI control values are restored on launch and saved on exit

---

## Design goals

- Simple, readable simulation core
- Deterministic runs (seeded RNG)
- Highly tweakable parameters:
  - evaporation rate
  - pheromone strength
  - turn chance and angle
  - sensor range
- Clear visualization of behavior
- Easy extension (multiple colonies, obstacles, etc.)

---

## Build and run

Prereqs: .NET 8 SDK.

```bash
dotnet build G33kColony.sln
dotnet run --project G33kColony.csproj
```

---

## Test

```bash
dotnet test G33kColony.sln
```

---

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
