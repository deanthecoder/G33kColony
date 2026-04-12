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
/// Represents an absolute point in the simulation world.
/// </summary>
public readonly record struct WorldPoint(double X, double Y)
{
    public static WorldPoint Zero { get; } = new(0, 0);

    public WorldPoint WithDelta(double deltaX, double deltaY) =>
        new(X + deltaX, Y + deltaY);

    public double DistanceSquared(WorldPoint other)
    {
        var deltaX = X - other.X;
        var deltaY = Y - other.Y;
        return deltaX * deltaX + deltaY * deltaY;
    }

    public double Distance(WorldPoint other) =>
        Math.Sqrt(DistanceSquared(other));
}

/// <summary>
/// Represents a single ant in the simulation world.
/// </summary>
public sealed class Ant
{
    private double m_headingRadians;
    private int m_stepsSincePheromoneDrop;

    public Ant(WorldPoint position, double directionX, double directionY)
    {
        Position = position;
        SetDirection(directionX, directionY);
    }

    public WorldPoint Position { get; private set; }

    public double HeadingRadians => m_headingRadians;

    public double DirectionX => Math.Cos(m_headingRadians);

    public double DirectionY => Math.Sin(m_headingRadians);

    public AntState State { get; private set; }

    public bool IsAlive { get; private set; } = true;

    public int LifeRemaining { get; private set; }

    public bool ShouldDropPheromone(int dropInterval) =>
        m_stepsSincePheromoneDrop % Math.Max(1, dropInterval) == 0;

    public void SetState(AntState state) =>
        State = state;

    internal void ResetLife(int maximumLife) =>
        LifeRemaining = Math.Max(1, maximumLife);

    internal void UseLife() =>
        LifeRemaining--;

    internal void Kill() =>
        IsAlive = false;

    internal void CountStep() =>
        m_stepsSincePheromoneDrop++;

    internal void Respawn(WorldPoint position, double headingRadians, int maximumLife)
    {
        Position = position;
        SetState(AntState.Searching);
        m_headingRadians = NormalizeRadians(headingRadians);
        m_stepsSincePheromoneDrop = 0;
        ResetLife(maximumLife);
        IsAlive = true;
    }

    internal void Turn(double radians) =>
        m_headingRadians = NormalizeRadians(m_headingRadians + radians);

    public void SetDirection(double directionX, double directionY)
    {
        if (Math.Abs(directionX) < double.Epsilon && Math.Abs(directionY) < double.Epsilon)
            return;

        m_headingRadians = NormalizeRadians(Math.Atan2(directionY, directionX));
    }

    public void MoveTo(WorldPoint position) =>
        Position = position;

    private static double NormalizeRadians(double radians)
    {
        while (radians <= -Math.PI)
            radians += Math.PI * 2;

        while (radians > Math.PI)
            radians -= Math.PI * 2;

        return radians;
    }
}

/// <summary>
/// Represents a finite circular food blob that searching ants can consume.
/// </summary>
public sealed class FoodSource
{
    private const double DetectionPadding = 3.5;

    public FoodSource(WorldPoint position, double radius, int amount)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");

        Position = position;
        Radius = radius;
        InitialAmount = amount;
        RemainingAmount = amount;
    }

    public WorldPoint Position { get; }

    public double Radius { get; }

    public int InitialAmount { get; }

    public int RemainingAmount { get; private set; }

    public bool IsDepleted => RemainingAmount <= 0;

    public bool Contains(WorldPoint point) =>
        !IsDepleted && Position.DistanceSquared(point) <= Math.Pow(Radius + DetectionPadding, 2);

    public bool TryConsume(WorldPoint point)
    {
        if (!Contains(point))
            return false;

        RemainingAmount--;
        return true;
    }
}

/// <summary>
/// Stores one pheromone mark in continuous world space.
/// </summary>
public sealed class PheromoneBlob
{
    public PheromoneBlob(WorldPoint position, double radius, float strength)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        if (strength < 0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must not be negative.");

        Position = position;
        Radius = radius;
        Strength = strength;
    }

    public WorldPoint Position { get; }

    public double Radius { get; }

    public float Strength { get; private set; }

    internal void Evaporate(float retention) =>
        Strength *= retention;
}

/// <summary>
/// Stores a collection of continuous pheromone blobs.
/// </summary>
public sealed class PheromoneField
{
    private const float MinimumStrength = 0.01f;
    private readonly List<PheromoneBlob> m_blobs = [];

    public IReadOnlyList<PheromoneBlob> Blobs => m_blobs;

    public void Add(WorldPoint position, double radius, float strength)
    {
        if (strength < 0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must not be negative.");

        m_blobs.Add(new PheromoneBlob(position, radius, strength));
    }

    public float SampleTotal(WorldPoint position, double radius)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        var total = 0f;
        var radiusSquared = radius * radius;
        for (var i = 0; i < m_blobs.Count; i++)
        {
            var blob = m_blobs[i];
            if (blob.Position.DistanceSquared(position) <= radiusSquared)
                total += blob.Strength;
        }

        return total;
    }

    public void Evaporate(float retention)
    {
        if (retention is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be between 0 and 1.");

        for (var i = m_blobs.Count - 1; i >= 0; i--)
        {
            var blob = m_blobs[i];
            blob.Evaporate(retention);
            if (blob.Strength < MinimumStrength)
                m_blobs.RemoveAt(i);
        }
    }
}

/// <summary>
/// Tracks the ants belonging to a single colony and advances their pheromone-led movement.
/// </summary>
public sealed class Colony
{
    public const double DefaultMaximumRandomTurnRadians = Math.PI / 4;
    public const double AntCollisionRadius = 5;
    public const double NestArrivalRadius = AntCollisionRadius;
    public const double SensorDistance = 12;
    public const double SensorRadius = 8;
    public const double SensorAngleRadians = Math.PI / 4;

    private const double StepDistance = 1.4;
    private const double PheromoneBlobRadius = 4.5;
    private const double SignalSteerFraction = 0.45;
    private const int PheromoneDropInterval = 4;
    private readonly List<Ant> m_ants;
    private readonly Random m_random;
    private readonly World m_world;
    private double m_turnChance = 0.55;
    private double m_maximumRandomTurnRadians = DefaultMaximumRandomTurnRadians;
    private float m_pheromoneDepositAmount = 2.4f;
    private int m_antMaximumLife = 1000;
    private bool m_hasFirstAntFoundFood;

    public Colony(World world, int antCount, int randomSeed = 1)
    {
        m_world = world ?? throw new ArgumentNullException(nameof(world));
        if (antCount < 0)
            throw new ArgumentOutOfRangeException(nameof(antCount), "Ant count must not be negative.");

        m_random = new Random(randomSeed);
        m_ants = new List<Ant>(antCount);

        for (var i = 0; i < antCount; i++)
            AddAntSlotAtNest();
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
        set => m_pheromoneDepositAmount = Math.Clamp(value, 0.001f, 8.0f);
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
    public Ant AddAnt(WorldPoint position, double directionX, double directionY)
    {
        if (!m_world.Contains(position))
            throw new ArgumentOutOfRangeException(nameof(position), $"Position {position} is outside the world.");

        var ant = CreateAnt(position, Math.Atan2(directionY, directionX));
        m_ants.Add(ant);
        return ant;
    }

    public void Tick()
    {
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive)
            {
                RespawnAnt(ant);
                continue;
            }

            Wander(ant);
        }
    }

    private void Wander(Ant ant)
    {
        var resetLifeThisTick = false;

        if (ant.ShouldDropPheromone(PheromoneDropInterval))
            DepositPheromone(ant);

        var scent = SampleScent(ant);
        var food = SampleFood(ant);
        var home = SampleHome(ant);
        var isFollowingDetectedFood = food.HasFood;
        var isFollowingDetectedHome = home.HasHome;
        var isFollowingScent = false;
        if (isFollowingDetectedFood)
            SteerToward(ant, food.Position);
        else if (isFollowingDetectedHome)
            SteerToward(ant, home.Position);
        else if (SteerByScent(ant, scent))
            isFollowingScent = true;

        if (ant.State == AntState.Returning &&
            ant.Position.DistanceSquared(m_world.NestPosition) <= SensorDistance * SensorDistance * 4)
        {
            SteerToward(ant, m_world.NestPosition);
            isFollowingDetectedHome = true;
        }

        var randomTurnChance = isFollowingDetectedFood ||
            isFollowingDetectedHome ||
            isFollowingScent && ant.State == AntState.Returning
                ? 0
                : GetScentAdjustedTurnChance(scent);
        if (m_random.NextDouble() < randomTurnChance)
            ant.Turn(CreateRandomTurnRadians());

        MoveForward(ant);

        if (ant.State == AntState.Searching && m_world.TryConsumeFood(ant.Position))
        {
            ant.SetState(AntState.Returning);
            FoodFoundCount++;
            ant.ResetLife(AntMaximumLife);
            ant.Turn(Math.PI);
            resetLifeThisTick = true;

            if (IsFirstAnt(ant) && !m_hasFirstAntFoundFood)
            {
                m_hasFirstAntFoundFood = true;
                Logger.Instance.Info($"First ant found food at {FormatPoint(ant.Position)}; nest={FormatPoint(m_world.NestPosition)}.");
            }
        }
        else if (ant.State == AntState.Returning && ant.Position.DistanceSquared(m_world.NestPosition) <= NestArrivalRadius * NestArrivalRadius)
        {
            ant.Respawn(m_world.NestPosition, CreateSpawnHeadingRadians(m_world.NestPosition), AntMaximumLife);
            FoodReturnedHomeCount++;
            resetLifeThisTick = true;
        }

        if (!resetLifeThisTick)
        {
            ant.UseLife();
            if (ant.LifeRemaining <= 0)
                RespawnAnt(ant);
        }

        ant.CountStep();
    }

    private void DepositPheromone(Ant ant)
    {
        var pheromones = ant.State == AntState.Searching
            ? m_world.HomePheromones
            : m_world.FoodPheromones;

        pheromones.Add(ant.Position, PheromoneBlobRadius, PheromoneDepositAmount);
    }

    private void MoveForward(Ant ant)
    {
        var next = ant.Position.WithDelta(ant.DirectionX * StepDistance, ant.DirectionY * StepDistance);
        if (!m_world.Contains(next))
        {
            ant.Turn(Math.PI);
            next = ant.Position.WithDelta(ant.DirectionX * StepDistance, ant.DirectionY * StepDistance);
        }

        ant.MoveTo(m_world.Clamp(next));
    }

    private Ant CreateAnt(WorldPoint position, double headingRadians)
    {
        var ant = new Ant(position, Math.Cos(headingRadians), Math.Sin(headingRadians));
        ant.ResetLife(AntMaximumLife);
        return ant;
    }

    private void AddAntSlotAtNest()
    {
        var ant = CreateAnt(m_world.NestPosition, CreateSpawnHeadingRadians(m_world.NestPosition));
        if (!m_world.HasFoodRemaining || IsSpawnBlocked(m_world.NestPosition, null))
            ant.Kill();

        m_ants.Add(ant);
    }

    private void RespawnAnt(Ant ant)
    {
        if (!m_world.HasFoodRemaining || IsSpawnBlocked(m_world.NestPosition, ant))
        {
            ant.Kill();
            return;
        }

        ant.Respawn(m_world.NestPosition, CreateSpawnHeadingRadians(m_world.NestPosition), AntMaximumLife);
    }

    private bool IsSpawnBlocked(WorldPoint spawnPosition, Ant spawningAnt)
    {
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive || ReferenceEquals(ant, spawningAnt))
                continue;

            if (ant.Position.DistanceSquared(spawnPosition) <= AntCollisionRadius * AntCollisionRadius)
                return true;
        }

        return false;
    }

    private static bool SteerByScent(Ant ant, ScentSample scent)
    {
        if (!scent.HasSignal)
            return false;

        ant.Turn(scent.SelectedAngleOffset * SignalSteerFraction);
        return true;
    }

    private static void SteerToward(Ant ant, WorldPoint target)
    {
        ant.SetDirection(target.X - ant.Position.X, target.Y - ant.Position.Y);
    }

    private ScentSample SampleScent(Ant ant)
    {
        var pheromones = ant.State == AntState.Searching
            ? m_world.FoodPheromones
            : m_world.HomePheromones;

        return SampleScent(ant, pheromones);
    }

    private FoodSample SampleFood(Ant ant)
    {
        if (ant.State != AntState.Searching)
            return new FoodSample(WorldPoint.Zero, false);

        var selectedPosition = WorldPoint.Zero;
        var selectedDistanceSquared = double.MaxValue;
        var hasFood = false;

        ConsiderFoodSample(ant, 0, ref selectedPosition, ref selectedDistanceSquared, ref hasFood);
        ConsiderFoodSample(ant, -SensorAngleRadians, ref selectedPosition, ref selectedDistanceSquared, ref hasFood);
        ConsiderFoodSample(ant, SensorAngleRadians, ref selectedPosition, ref selectedDistanceSquared, ref hasFood);

        return new FoodSample(selectedPosition, hasFood);
    }

    private void ConsiderFoodSample(
        Ant ant,
        double angleOffset,
        ref WorldPoint selectedPosition,
        ref double selectedDistanceSquared,
        ref bool hasFood)
    {
        var samplePosition = GetSamplePosition(ant, angleOffset);
        foreach (var source in m_world.FoodSources)
        {
            if (source.IsDepleted)
                continue;

            var detectionRadius = SensorRadius + source.Radius;
            if (source.Position.DistanceSquared(samplePosition) > detectionRadius * detectionRadius)
                continue;

            var distanceSquared = source.Position.DistanceSquared(ant.Position);
            if (hasFood && distanceSquared >= selectedDistanceSquared)
                continue;

            selectedPosition = source.Position;
            selectedDistanceSquared = distanceSquared;
            hasFood = true;
        }
    }

    private HomeSample SampleHome(Ant ant)
    {
        if (ant.State != AntState.Returning)
            return new HomeSample(WorldPoint.Zero, false);

        return IsHomeInSampleArea(ant, 0) ||
            IsHomeInSampleArea(ant, -SensorAngleRadians) ||
            IsHomeInSampleArea(ant, SensorAngleRadians)
                ? new HomeSample(m_world.NestPosition, true)
                : new HomeSample(WorldPoint.Zero, false);
    }

    private bool IsHomeInSampleArea(Ant ant, double angleOffset)
    {
        var samplePosition = GetSamplePosition(ant, angleOffset);
        var detectionRadius = SensorRadius + NestArrivalRadius;
        return m_world.NestPosition.DistanceSquared(samplePosition) <= detectionRadius * detectionRadius;
    }

    private static ScentSample SampleScent(Ant ant, PheromoneField pheromones)
    {
        var leftStrength = SampleAhead(ant, pheromones, -SensorAngleRadians);
        var straightStrength = SampleAhead(ant, pheromones, 0);
        var rightStrength = SampleAhead(ant, pheromones, SensorAngleRadians);
        var selectedAngle = 0.0;
        var selectedStrength = 0f;
        var hasSignal = false;

        ConsiderScentSample(0, straightStrength, ref selectedAngle, ref selectedStrength, ref hasSignal);
        ConsiderScentSample(-SensorAngleRadians, leftStrength, ref selectedAngle, ref selectedStrength, ref hasSignal);
        ConsiderScentSample(SensorAngleRadians, rightStrength, ref selectedAngle, ref selectedStrength, ref hasSignal);

        return new ScentSample(selectedAngle, selectedStrength, hasSignal);
    }

    private static void ConsiderScentSample(
        double angle,
        float strength,
        ref double selectedAngle,
        ref float selectedStrength,
        ref bool hasSignal)
    {
        if (strength <= 0)
            return;
        if (hasSignal && strength <= selectedStrength)
            return;
        selectedAngle = angle;
        selectedStrength = strength;
        hasSignal = true;
    }

    private static float SampleAhead(Ant ant, PheromoneField pheromones, double angleOffset)
    {
        var samplePosition = GetSamplePosition(ant, angleOffset);
        return pheromones.SampleTotal(samplePosition, SensorRadius);
    }

    private static WorldPoint GetSamplePosition(Ant ant, double angleOffset)
    {
        var sampleHeading = ant.HeadingRadians + angleOffset;
        return ant.Position.WithDelta(
            Math.Cos(sampleHeading) * SensorDistance,
            Math.Sin(sampleHeading) * SensorDistance);
    }

    private double GetScentAdjustedTurnChance(ScentSample scent)
    {
        if (!scent.HasSignal)
            return TurnChance;

        return TurnChance / (1 + Math.Max(1, scent.SelectedStrength) * 8);
    }

    private bool IsFirstAnt(Ant ant) =>
        m_ants.Count > 0 && ReferenceEquals(m_ants[0], ant);
    
    private static string FormatPoint(WorldPoint point) =>
        $"({point.X:0.##},{point.Y:0.##})";

    private double CreateRandomTurnRadians() =>
        (m_random.NextDouble() * 2 - 1) * MaximumRandomTurnRadians;

    private double CreateRandomHeadingRadians() =>
        m_random.NextDouble() * Math.PI * 2;

    private double CreateSpawnHeadingRadians(WorldPoint position)
    {
        var target = SampleNearbyPheromoneTarget(position, m_world.FoodPheromones);
        if (target.HasTarget)
            return Math.Atan2(target.Position.Y - position.Y, target.Position.X - position.X);

        return CreateRandomHeadingRadians();
    }

    private static PheromoneTarget SampleNearbyPheromoneTarget(WorldPoint position, PheromoneField pheromones)
    {
        var selectedPosition = WorldPoint.Zero;
        var selectedStrength = 0f;
        var selectedDistanceSquared = double.MaxValue;
        var hasTarget = false;

        foreach (var blob in pheromones.Blobs)
        {
            var detectionRadius = SensorDistance + SensorRadius + blob.Radius;
            var distanceSquared = blob.Position.DistanceSquared(position);
            if (distanceSquared <= double.Epsilon ||
                distanceSquared > detectionRadius * detectionRadius)
            {
                continue;
            }

            if (hasTarget &&
                (blob.Strength < selectedStrength ||
                    blob.Strength == selectedStrength && distanceSquared >= selectedDistanceSquared))
            {
                continue;
            }

            selectedPosition = blob.Position;
            selectedStrength = blob.Strength;
            selectedDistanceSquared = distanceSquared;
            hasTarget = true;
        }

        return new PheromoneTarget(selectedPosition, hasTarget);
    }

    private readonly record struct ScentSample(
        double SelectedAngleOffset,
        float SelectedStrength,
        bool HasSignal);

    private readonly record struct FoodSample(WorldPoint Position, bool HasFood);

    private readonly record struct HomeSample(WorldPoint Position, bool HasHome);

    private readonly record struct PheromoneTarget(WorldPoint Position, bool HasTarget);
}

/// <summary>
/// Contains fixed-size simulation data for the ant colony world.
/// </summary>
public sealed class World
{
    public const int DefaultWidth = 640;
    public const int DefaultHeight = 480;
    public const int FoodBlobsPerSource = 40;

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
        NestPosition = new WorldPoint(width / 2.0, height / 2.0);
        HomePheromones = new PheromoneField();
        FoodPheromones = new PheromoneField();
        FoodSources = CreateFoodSources(foodSourceCount, randomSeed);
    }

    public int Width { get; }

    public int Height { get; }

    public WorldPoint NestPosition { get; }

    public PheromoneField HomePheromones { get; }

    public PheromoneField FoodPheromones { get; }

    public IReadOnlyList<FoodSource> FoodSources { get; }

    public bool HasFoodRemaining
    {
        get
        {
            foreach (var source in FoodSources)
            {
                if (!source.IsDepleted)
                    return true;
            }

            return false;
        }
    }

    public bool IsFood(WorldPoint position)
    {
        foreach (var source in FoodSources)
        {
            if (source.Contains(position))
                return true;
        }

        return false;
    }

    public bool TryConsumeFood(WorldPoint position)
    {
        foreach (var source in FoodSources)
        {
            if (source.TryConsume(position))
                return true;
        }

        return false;
    }

    public bool Contains(WorldPoint position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public WorldPoint Clamp(WorldPoint position) =>
        new(Math.Clamp(position.X, 0, Width - 1), Math.Clamp(position.Y, 0, Height - 1));

    public void Tick(float pheromoneRetention)
    {
        HomePheromones.Evaporate(pheromoneRetention);
        FoodPheromones.Evaporate(pheromoneRetention);
    }

    private IReadOnlyList<FoodSource> CreateFoodSources(int foodSourceCount, int randomSeed)
    {
        var sources = new List<FoodSource>(foodSourceCount * FoodBlobsPerSource);
        var random = new Random(randomSeed);
        var blobRadius = Math.Clamp(Math.Min(Width, Height) / 110.0, 1.2, 2.8);
        var vicinityRadius = Math.Clamp(Math.Min(Width, Height) / 16.0, blobRadius * 5, 28);
        var minimumClusterDistanceSquared = Math.Pow(vicinityRadius * 2, 2);
        var minimumNestDistanceSquared = Math.Max(minimumClusterDistanceSquared, Math.Pow(Math.Min(Width, Height) * 0.22, 2));
        var clusterCenters = new List<WorldPoint>(foodSourceCount);

        for (var i = 0; i < foodSourceCount; i++)
        {
            var center = CreateFoodSourcePosition(random, vicinityRadius, minimumNestDistanceSquared, minimumClusterDistanceSquared, clusterCenters);
            clusterCenters.Add(center);
            AddFoodBlobCluster(random, sources, center, vicinityRadius, blobRadius);
        }

        return sources;
    }

    private void AddFoodBlobCluster(
        Random random,
        List<FoodSource> sources,
        WorldPoint center,
        double vicinityRadius,
        double blobRadius)
    {
        for (var i = 0; i < FoodBlobsPerSource; i++)
        {
            var distance = Math.Sqrt(random.NextDouble()) * vicinityRadius;
            var angle = random.NextDouble() * Math.PI * 2;
            var position = Clamp(center.WithDelta(
                Math.Cos(angle) * distance,
                Math.Sin(angle) * distance));
            sources.Add(new FoodSource(position, blobRadius, 1));
        }
    }

    private WorldPoint CreateFoodSourcePosition(
        Random random,
        double margin,
        double minimumNestDistanceSquared,
        double minimumDistanceSquared,
        List<WorldPoint> existingCenters)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var minX = Math.Min(margin, Width - 1);
            var minY = Math.Min(margin, Height - 1);
            var maxX = Math.Max(minX + 1, Width - margin);
            var maxY = Math.Max(minY + 1, Height - margin);
            var position = new WorldPoint(
                Math.Clamp(random.NextDouble() * (maxX - minX) + minX, 0, Width - 1),
                Math.Clamp(random.NextDouble() * (maxY - minY) + minY, 0, Height - 1));

            if (position.DistanceSquared(NestPosition) < minimumNestDistanceSquared)
                continue;

            var tooCloseToOtherSource = false;
            foreach (var center in existingCenters)
            {
                if (position.DistanceSquared(center) < minimumDistanceSquared)
                {
                    tooCloseToOtherSource = true;
                    break;
                }
            }

            if (!tooCloseToOtherSource)
                return position;
        }

        return new WorldPoint(Math.Clamp(Width - margin - 1, 0, Width - 1), Math.Clamp(margin, 0, Height - 1));
    }
}
