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

The world is a 2D grid of cells.

Each cell may contain:
- food (finite amount)
- pheromones:
  - **home pheromone**
  - **food pheromone**

Each ant has:
- a position
- a direction (with momentum)
- a state:
  - `Searching`
  - `Returning`

---

## Algorithm (Dual pheromone system)

### Searching ants

- Leave the nest and perform a biased random walk:
  - maintain general direction (momentum)
  - apply small random angle turns that accumulate over time
- Deposit **home pheromone**
- Sample nearby cells in a 5x5 scent window:
  - if **food pheromone** is detected, bias movement toward the averaged stronger values
  - random turns still happen, but become less likely when the food scent is stronger
- If food is found:
  - pick up food
  - switch to `Returning`

---

### Returning ants

- Move toward the nest by following **home pheromone**
- Deposit **food pheromone**
- If the nest is reached:
  - drop food
  - switch to `Searching`

---

### Pheromone behaviour

- Both pheromones:
  - **evaporate over time**
  - may **diffuse slightly** into neighbouring cells
- Strong, frequently used paths become reinforced
- Weak or unused paths fade away

---

## Movement model

At each simulation tick:

1. Each ant:
   - samples pheromone levels in a small 5x5 scent window
   - selects a direction based on:
     - pheromone strength
     - momentum
     - small random angle changes
   - moves forward one step
   - deposits pheromone depending on state

2. The world:
   - applies pheromone evaporation
   - optionally applies diffusion

This loop produces emergent pathfinding without any explicit pathfinding algorithm.

---

## Rendering

The simulation runs on a grid, but rendering will be higher resolution:

- each cell rendered as a small block (e.g. 3x3 pixels)
- pheromones visualised as intensity/colour overlays
- ants rendered as moving points

Potential overlays:
- food pheromone
- home pheromone
- ant paths / density

Current first pass:
- fixed 640x480 world cells
- bitmap renderer scaled uniformly to the window
- menu toggles for home and food pheromone visibility
- top-right controls for seed, restart, decay, trail strength, turn chance, turn angle, simulation speed, ant count, and food source count

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
