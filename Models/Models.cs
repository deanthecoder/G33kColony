// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using DTC.Core;

namespace G33kColony.Models;

/// <summary>
/// Describes the current behaviour mode for an ant.
/// </summary>
public enum AntState
{
    Searching,
    Returning
}

/// <summary>
/// Represents a single ant in the simulation world.
/// </summary>
public sealed class Ant
{
    private double m_headingRadians;

    public Ant(IntPoint position, int directionX, int directionY)
    {
        Position = position;
        SetDirection(directionX, directionY);
    }

    public IntPoint Position { get; private set; }

    public int DirectionX => GetCellDirection().X;

    public int DirectionY => GetCellDirection().Y;

    public AntState State { get; private set; }

    public bool IsAlive { get; private set; } = true;

    public int LifeRemaining { get; private set; }

    public int PreviousDirectionX { get; private set; }

    public int PreviousDirectionY { get; private set; }

    public void SetState(AntState state) =>
        State = state;

    internal void ResetLife(int maximumLife) =>
        LifeRemaining = Math.Max(1, maximumLife);

    internal void UseLife() =>
        LifeRemaining--;

    internal void Kill() =>
        IsAlive = false;

    internal void Respawn(IntPoint position, int directionX, int directionY, int maximumLife)
    {
        Position = position;
        SetState(AntState.Searching);
        SetDirection(directionX, directionY);
        ResetLife(maximumLife);
        IsAlive = true;
    }

    internal void Turn(double radians) =>
        m_headingRadians = NormalizeRadians(m_headingRadians + radians);

    public void SetDirection(int directionX, int directionY)
    {
        var currentDirection = GetCellDirection();
        PreviousDirectionX = currentDirection.X;
        PreviousDirectionY = currentDirection.Y;

        var newDirectionX = Math.Clamp(directionX, -1, 1);
        var newDirectionY = Math.Clamp(directionY, -1, 1);

        if (newDirectionX == 0 && newDirectionY == 0)
            return;

        m_headingRadians = Math.Atan2(newDirectionY, newDirectionX);
    }

    public void MoveTo(IntPoint position) =>
        Position = position;

    private static double NormalizeRadians(double radians)
    {
        while (radians <= -Math.PI)
            radians += Math.PI * 2;

        while (radians > Math.PI)
            radians -= Math.PI * 2;

        return radians;
    }

    private (int X, int Y) GetCellDirection()
    {
        var sector = (int)Math.Round(m_headingRadians / (Math.PI / 4), MidpointRounding.ToEven);
        sector = ((sector % 8) + 8) % 8;

        return sector switch
        {
            0 => (1, 0),
            1 => (1, 1),
            2 => (0, 1),
            3 => (-1, 1),
            4 => (-1, 0),
            5 => (-1, -1),
            6 => (0, -1),
            _ => (1, -1)
        };
    }
}

/// <summary>
/// Represents a circular food blob that searching ants can discover.
/// </summary>
public sealed class FoodSource
{
    public FoodSource(IntPoint position, int radius)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        Position = position;
        Radius = radius;
    }

    public IntPoint Position { get; }

    public int Radius { get; }

    public bool Contains(IntPoint point) =>
        Position.DistanceSquared(point) <= Radius * Radius;
}

/// <summary>
/// Stores one scalar pheromone layer for every cell in the world.
/// </summary>
public sealed class PheromoneData
{
    private readonly int m_width;
    private readonly int m_height;
    private readonly float[] m_cells;

    public PheromoneData(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");

        m_width = width;
        m_height = height;
        m_cells = new float[width * height];
    }
    
    public ReadOnlySpan<float> Cells => m_cells;

    public float this[int x, int y]
    {
        get => m_cells[GetIndex(x, y)];
        set => m_cells[GetIndex(x, y)] = Math.Max(0, value);
    }

    public void Add(int x, int y, float amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");

        m_cells[GetIndex(x, y)] += amount;
    }

    public void AddTowardsMax(int x, int y, float amount, float maximum)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");
        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must not be negative.");

        var index = GetIndex(x, y);
        var current = m_cells[index];
        var remaining = Math.Max(0, maximum - current);
        m_cells[index] = current + remaining * amount;
    }

    public void Evaporate(float retention)
    {
        if (retention is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be between 0 and 1.");

        for (var i = 0; i < m_cells.Length; i++)
            m_cells[i] *= retention;
    }

    public bool Contains(int x, int y) =>
        x >= 0 && y >= 0 && x < m_width && y < m_height;

    private int GetIndex(int x, int y)
    {
        if (!Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside {m_width}x{m_height}.");

        return y * m_width + x;
    }
}

/// <summary>
/// Tracks the ants belonging to a single colony and advances their simple movement.
/// </summary>
public sealed class Colony
{
    public const double DefaultMaximumRandomTurnRadians = Math.PI / 4;
    
    private const float MinimumFollowableScent = 0.05f;
    private const int ScentRadius = 2;
    private const int NestArrivalRadius = 2;
    private const int SpawnClearRadius = 5;
    private const float MaximumPheromoneStrength = 2.0f;
    private const float CenterDepositWeight = 1.0f;
    private const float OrthogonalDepositWeight = 0.45f;
    private const float DiagonalDepositWeight = 0.2f;
    private readonly List<Ant> m_ants;
    private readonly Random m_random;
    private readonly World m_world;
    private double m_turnChance = 0.55;
    private double m_maximumRandomTurnRadians = DefaultMaximumRandomTurnRadians;
    private float m_pheromoneDepositAmount = 0.35f;
    private int m_antMaximumLife = 1000;
    private bool m_isFirstAntFoodTraceActive;
    private bool m_hasFirstAntFoundFood;

    public Colony(World world, int antCount, int randomSeed = 1)
    {
        m_world = world ?? throw new ArgumentNullException(nameof(world));
        if (antCount < 0)
            throw new ArgumentOutOfRangeException(nameof(antCount), "Ant count must not be negative.");

        m_random = new Random(randomSeed);
        m_ants = new List<Ant>(antCount);

        for (var i = 0; i < antCount; i++)
        {
            var (directionX, directionY) = CreateRandomDirection();
            m_ants.Add(CreateAnt(world.NestPosition, directionX, directionY));
        }
    }

    public IReadOnlyList<Ant> Ants => m_ants;

    public double TurnChance
    {
        get => m_turnChance;
        set => m_turnChance = Math.Clamp(value, 0, 1);
    }

    public double MaximumRandomTurnRadians
    {
        get => m_maximumRandomTurnRadians;
        set => m_maximumRandomTurnRadians = Math.Clamp(value, Math.PI / 36, Math.PI * 5 / 12);
    }

    public float PheromoneDepositAmount
    {
        get => m_pheromoneDepositAmount;
        set => m_pheromoneDepositAmount = Math.Clamp(value, 0.001f, 1.0f);
    }

    public int AntMaximumLife
    {
        get => m_antMaximumLife;
        set
        {
            var maximumLife = Math.Max(1, value);
            if (m_antMaximumLife == maximumLife)
                return;

            m_antMaximumLife = maximumLife;
            foreach (var ant in m_ants)
                ant.ResetLife(m_antMaximumLife);
        }
    }

    public int FoodFoundCount { get; private set; }

    public int FoodReturnedHomeCount { get; private set; }

    /// <summary>
    /// Adds an ant to the colony at a specific world position.
    /// </summary>
    public Ant AddAnt(IntPoint position, int directionX, int directionY)
    {
        if (!m_world.Contains(position))
            throw new ArgumentOutOfRangeException(nameof(position), $"Position {position} is outside the world.");

        var ant = CreateAnt(position, directionX, directionY);
        m_ants.Add(ant);
        return ant;
    }

    public void Tick()
    {
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive)
            {
                TryRespawnAnt(ant);
                continue;
            }

            DepositPheromone(ant);
            Wander(ant);
        }
    }

    private void DepositPheromone(Ant ant)
    {
        var pheromones = ant.State == AntState.Searching
            ? m_world.HomePheromones
            : m_world.FoodPheromones;

        DepositPheromoneKernel(pheromones, ant.Position.X, ant.Position.Y, PheromoneDepositAmount);
    }

    private void DepositPheromoneKernel(PheromoneData pheromones, int centerX, int centerY, float baseAmount)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            var x = centerX + offsetX;
            var y = centerY + offsetY;
            if (!pheromones.Contains(x, y))
                continue;

            var weight = GetDepositWeight(offsetX, offsetY);
            if (weight <= 0)
                continue;

            pheromones.AddTowardsMax(x, y, baseAmount * weight, MaximumPheromoneStrength);
        }
    }

    private static float GetDepositWeight(int offsetX, int offsetY)
    {
        if (offsetX == 0 && offsetY == 0)
            return CenterDepositWeight;

        if (offsetX == 0 || offsetY == 0)
            return OrthogonalDepositWeight;

        return DiagonalDepositWeight;
    }

    private void Wander(Ant ant)
    {
        var previousPosition = ant.Position;
        var previousState = ant.State;
        var scent = SampleScent(ant);
        var randomTurnChance = GetScentAdjustedTurnChance(scent.TotalScent);
        var action = "move";
        var stopFirstAntFoodTraceAfterLog = false;
        var resetLifeThisTick = false;

        if (m_random.NextDouble() < randomTurnChance)
        {
            ant.Turn(CreateRandomTurnRadians());
            action = "random turn";
        }

        var returningFromFood = ant.State == AntState.Returning && m_world.IsFood(ant.Position);
        var followedScent = !returningFromFood && SteerByScent(ant, scent);
        if (followedScent)
            action = $"follow {GetScentLayerName(previousState)} scent";

        if (returningFromFood || ant.State == AntState.Returning && !followedScent && m_random.NextDouble() < 0.75)
        {
            SteerToward(ant, m_world.NestPosition);
            action = returningFromFood ? "leave food" : "steer home";
        }

        var next = ant.Position.WithDelta(ant.DirectionX, ant.DirectionY);
        if (!m_world.Contains(next))
        {
            ant.SetDirection(-ant.DirectionX, -ant.DirectionY);
            next = ant.Position.WithDelta(ant.DirectionX, ant.DirectionY);
            action = "turn from wall";
        }

        next = ResolveMoveTarget(ant, m_world.Clamp(next));
        if (next == ant.Position)
            action = "blocked";

        ant.MoveTo(next);

        if (ant.State == AntState.Searching && m_world.IsFood(ant.Position))
        {
            ant.SetState(AntState.Returning);
            FoodFoundCount++;
            ant.ResetLife(AntMaximumLife);
            resetLifeThisTick = true;

            var reverseDirectionX = -ant.PreviousDirectionX;
            var reverseDirectionY = -ant.PreviousDirectionY;
            if (reverseDirectionX == 0 && reverseDirectionY == 0)
            {
                reverseDirectionX = -ant.DirectionX;
                reverseDirectionY = -ant.DirectionY;
            }

            ant.SetDirection(reverseDirectionX, reverseDirectionY);
            action = "found food";

            if (IsFirstAnt(ant) && !m_hasFirstAntFoundFood)
            {
                m_hasFirstAntFoundFood = true;
                m_isFirstAntFoodTraceActive = true;
                Logger.Instance.Info($"First ant found food at {ant.Position}; nest={m_world.NestPosition}.");
            }
        }
        else if (ant.State == AntState.Returning && ant.Position.DistanceSquared(m_world.NestPosition) <= NestArrivalRadius * NestArrivalRadius)
        {
            DepositPheromoneKernel(m_world.FoodPheromones, ant.Position.X, ant.Position.Y, PheromoneDepositAmount);
            ant.SetState(AntState.Searching);
            FoodReturnedHomeCount++;
            action = "returned home";

            if (IsFirstAnt(ant))
                stopFirstAntFoodTraceAfterLog = true;
        }

        if (m_isFirstAntFoodTraceActive && IsFirstAnt(ant))
            LogFirstAntFoodTrace(ant, previousPosition, previousState, scent, action);

        if (stopFirstAntFoodTraceAfterLog)
            m_isFirstAntFoodTraceActive = false;

        if (!resetLifeThisTick)
        {
            ant.UseLife();
            if (ant.LifeRemaining <= 0)
                TryRespawnAnt(ant);
        }
    }

    private Ant CreateAnt(IntPoint position, int directionX, int directionY)
    {
        var ant = new Ant(position, directionX, directionY);
        ant.ResetLife(AntMaximumLife);
        return ant;
    }

    private IntPoint ResolveMoveTarget(Ant ant, IntPoint preferredPosition)
    {
        if (IsCellEmpty(preferredPosition, ant))
            return preferredPosition;

        var bestPosition = ant.Position;
        var bestDistanceSquared = double.MaxValue;

        for (var y = ant.Position.Y - 1; y <= ant.Position.Y + 1; y++)
        for (var x = ant.Position.X - 1; x <= ant.Position.X + 1; x++)
        {
            var candidate = new IntPoint(x, y);
            if (candidate == ant.Position || !m_world.Contains(candidate) || !IsCellEmpty(candidate, ant))
                continue;

            var distanceSquared = candidate.DistanceSquared(preferredPosition);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestPosition = candidate;
            bestDistanceSquared = distanceSquared;
        }

        return bestPosition;
    }

    private bool IsCellEmpty(IntPoint position, Ant movingAnt)
    {
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive || ReferenceEquals(ant, movingAnt))
                continue;

            if (ant.Position == position)
                return false;
        }

        return true;
    }

    private bool TryRespawnAnt(Ant ant)
    {
        if (IsSpawnBlocked(m_world.NestPosition, ant))
        {
            ant.Kill();
            return false;
        }

        var (directionX, directionY) = CreateRandomDirection();
        ant.Respawn(m_world.NestPosition, directionX, directionY, AntMaximumLife);
        return true;
    }

    private bool IsSpawnBlocked(IntPoint spawnPosition, Ant spawningAnt)
    {
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive || ReferenceEquals(ant, spawningAnt))
                continue;

            if (ant.Position.DistanceSquared(spawnPosition) <= SpawnClearRadius * SpawnClearRadius)
                return true;
        }

        return false;
    }

    private static void SteerToward(Ant ant, IntPoint target)
    {
        var directionX = Math.Sign(target.X - ant.Position.X);
        var directionY = Math.Sign(target.Y - ant.Position.Y);
        ant.SetDirection(directionX, directionY);
    }

    private static bool SteerByScent(Ant ant, ScentSample scent)
    {
        if (!scent.HasDirection)
            return false;

        ant.SetDirection(Math.Sign(scent.SelectedX), Math.Sign(scent.SelectedY));
        return true;
    }

    private ScentSample SampleScent(Ant ant)
    {
        var pheromones = ant.State == AntState.Searching
            ? m_world.FoodPheromones
            : m_world.HomePheromones;

        return SampleScent(ant, pheromones, ant.State);
    }

    private double GetScentAdjustedTurnChance(float scentStrength) =>
        TurnChance / (1 + scentStrength * 6);

    private bool IsFirstAnt(Ant ant) =>
        m_ants.Count > 0 && ReferenceEquals(m_ants[0], ant);

    private void LogFirstAntFoodTrace(
        Ant ant,
        IntPoint previousPosition,
        AntState previousState,
        ScentSample scent,
        string action)
    {
        var homeScent = SampleScent(ant, m_world.HomePheromones, AntState.Returning);
        var foodScent = SampleScent(ant, m_world.FoodPheromones, AntState.Searching);
        Logger.Instance.Info(
            $"First ant trace: action={action}; state={previousState}->{ant.State}; " +
            $"position={previousPosition}->{ant.Position}; direction=({ant.DirectionX},{ant.DirectionY}); " +
            $"distanceHome={ant.Position.DistanceSquared(m_world.NestPosition):0.##}; isFood={m_world.IsFood(ant.Position)}; " +
            $"{GetScentLayerName(previousState)}ScentBefore={FormatScent(scent)}; " +
            $"homeScentHere={FormatScent(homeScent)}; foodScentHere={FormatScent(foodScent)}.");
    }

    private static ScentSample SampleScent(Ant ant, PheromoneData pheromones, AntState state)
    {
        var totalScent = 0f;
        var hasBestDirection = false;
        var selectedScent = 0f;
        var selectedX = 0;
        var selectedY = 0;
        var directionX = ant.DirectionX;
        var directionY = ant.DirectionY;
        var currentScent = pheromones.Contains(ant.Position.X, ant.Position.Y)
            ? pheromones[ant.Position.X, ant.Position.Y]
            : 0f;

        for (var y = ant.Position.Y - ScentRadius; y <= ant.Position.Y + ScentRadius; y++)
        for (var x = ant.Position.X - ScentRadius; x <= ant.Position.X + ScentRadius; x++)
        {
            var relativeX = x - ant.Position.X;
            var relativeY = y - ant.Position.Y;
            if (relativeX == 0 && relativeY == 0 || !pheromones.Contains(x, y))
                continue;

            if (relativeX * directionX + relativeY * directionY <= 0)
                continue;

            var cellScent = pheromones[x, y];
            if (cellScent <= MinimumFollowableScent)
                continue;

            totalScent += cellScent;

            if (state == AntState.Returning && cellScent >= currentScent)
                continue;

            if (!hasBestDirection)
            {
                hasBestDirection = true;
                selectedScent = cellScent;
                selectedX = relativeX;
                selectedY = relativeY;
                continue;
            }

            var shouldReplace = state == AntState.Returning
                ? cellScent > selectedScent
                : cellScent < selectedScent;

            if (shouldReplace ||
                cellScent == selectedScent && IsBetterScentTieBreak(ant, relativeX, relativeY, selectedX, selectedY))
            {
                selectedScent = cellScent;
                selectedX = relativeX;
                selectedY = relativeY;
            }
        }

        return new ScentSample(totalScent, selectedX, selectedY, selectedScent, hasBestDirection);
    }

    private static string FormatScent(ScentSample scent) =>
        $"total={scent.TotalScent:0.###}, selected=({scent.SelectedX},{scent.SelectedY})/{scent.SelectedScent:0.###}, hasDirection={scent.HasDirection}";

    private static string GetScentLayerName(AntState state) =>
        state == AntState.Searching ? "food" : "home";

    private static string GetScentSelectionRuleName(AntState state) =>
        state == AntState.Searching ? "lowest positive ahead" : "highest below current";

    private static bool IsBetterScentTieBreak(
        Ant ant,
        int relativeX,
        int relativeY,
        int selectedX,
        int selectedY)
    {
        var antDirectionX = ant.DirectionX;
        var antDirectionY = ant.DirectionY;

        var candidateAlignment = relativeX * antDirectionX + relativeY * antDirectionY;
        var selectedAlignment = selectedX * antDirectionX + selectedY * antDirectionY;
        return candidateAlignment > selectedAlignment;
    }
    
    private (int DirectionX, int DirectionY) CreateRandomDirection()
    {
        while (true)
        {
            var directionX = m_random.Next(-1, 2);
            var directionY = m_random.Next(-1, 2);
            if (directionX != 0 || directionY != 0)
                return (directionX, directionY);
        }
    }

    private double CreateRandomTurnRadians() =>
        (m_random.NextDouble() * 2 - 1) * MaximumRandomTurnRadians;

    private readonly record struct ScentSample(
        float TotalScent,
        int SelectedX,
        int SelectedY,
        float SelectedScent,
        bool HasDirection)
    {
    }
}

/// <summary>
/// Contains fixed-size simulation data for the ant colony world.
/// </summary>
public sealed class World
{
    public const int DefaultWidth = 640;
    public const int DefaultHeight = 480;

    public World()
        : this(DefaultWidth, DefaultHeight)
    {
    }

    public World(int width, int height, int foodSourceCount = 1, int randomSeed = 1)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");

        Width = width;
        Height = height;
        NestPosition = new IntPoint(width / 2, height / 2);
        HomePheromones = new PheromoneData(width, height);
        FoodPheromones = new PheromoneData(width, height);
        FoodSources = CreateFoodSources(foodSourceCount, randomSeed);
    }

    public int Width { get; }

    public int Height { get; }

    public IntPoint NestPosition { get; }

    public PheromoneData HomePheromones { get; }

    public PheromoneData FoodPheromones { get; }

    public IReadOnlyList<FoodSource> FoodSources { get; }

    public bool IsFood(IntPoint position)
    {
        foreach (var source in FoodSources)
        {
            if (source.Contains(position))
                return true;
        }

        return false;
    }

    public bool Contains(IntPoint position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public IntPoint Clamp(IntPoint position) =>
        new(Math.Clamp(position.X, 0, Width - 1), Math.Clamp(position.Y, 0, Height - 1));

    public void Tick(float pheromoneRetention)
    {
        HomePheromones.Evaporate(pheromoneRetention);
        FoodPheromones.Evaporate(pheromoneRetention);
    }

    private IReadOnlyList<FoodSource> CreateFoodSources(int foodSourceCount, int randomSeed)
    {
        var sources = new List<FoodSource>(foodSourceCount);
        var random = new Random(randomSeed);
        var maxRadius = Math.Max(1, Math.Min(22, Math.Min(Width, Height) / 3));
        var preferredRadius = Math.Max(1, Math.Min(Width, Height) / 28);
        var radius = Math.Clamp(Math.Max(preferredRadius, Math.Min(8, maxRadius)), 1, maxRadius);
        var minimumDistanceSquared = Math.Pow(radius * 2, 2);
        var minimumNestDistanceSquared = Math.Max(minimumDistanceSquared, Math.Pow(Math.Min(Width, Height) * 0.22, 2));

        for (var i = 0; i < foodSourceCount; i++)
        {
            var position = CreateFoodSourcePosition(random, radius, minimumNestDistanceSquared, minimumDistanceSquared, sources);
            sources.Add(new FoodSource(position, radius));
        }

        return sources;
    }

    private IntPoint CreateFoodSourcePosition(
        Random random,
        int radius,
        double minimumNestDistanceSquared,
        double minimumDistanceSquared,
        List<FoodSource> existingSources)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var minX = Math.Min(radius, Width - 1);
            var minY = Math.Min(radius, Height - 1);
            var maxX = Math.Max(minX + 1, Width - radius);
            var maxY = Math.Max(minY + 1, Height - radius);
            var position = new IntPoint(
                Math.Clamp(random.Next(minX, maxX), 0, Width - 1),
                Math.Clamp(random.Next(minY, maxY), 0, Height - 1));

            if (position.DistanceSquared(NestPosition) < minimumNestDistanceSquared)
                continue;

            var tooCloseToOtherSource = false;
            foreach (var source in existingSources)
            {
                if (position.DistanceSquared(source.Position) < minimumDistanceSquared)
                {
                    tooCloseToOtherSource = true;
                    break;
                }
            }

            if (!tooCloseToOtherSource)
                return position;
        }

        return new IntPoint(Math.Clamp(Width - radius - 1, 0, Width - 1), Math.Clamp(radius, 0, Height - 1));
    }
}
