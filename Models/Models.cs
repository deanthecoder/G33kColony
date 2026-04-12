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
    private double m_desiredHeadingRadians;
    private int m_stepsSincePheromoneDrop;
    private int m_launchTicksRemaining;

    public Ant(WorldPoint position, double directionX, double directionY)
    {
        Position = position;
        SetDirection(directionX, directionY);
    }

    public WorldPoint Position { get; private set; }

    public double HeadingRadians { get; private set; }

    public double DirectionX => Math.Cos(HeadingRadians);

    public double DirectionY => Math.Sin(HeadingRadians);

    public AntState State { get; private set; }

    public bool IsAlive { get; private set; } = true;

    public int MaximumLife { get; private set; }

    public int LifeRemaining { get; private set; }

    public double Speed { get; private set; } = 1;

    public bool IsInLaunchPhase => m_launchTicksRemaining > 0;

    public bool ShouldDropPheromone(int dropInterval) =>
        m_stepsSincePheromoneDrop % Math.Max(1, dropInterval) == 0;

    public void SetState(AntState state) =>
        State = state;

    internal void ResetLife(int maximumLife)
    {
        MaximumLife = Math.Max(1, maximumLife);
        RefreshLife();
    }

    internal void RefreshLife() =>
        LifeRemaining = MaximumLife;

    internal void SetSpeed(double speed) =>
        Speed = Math.Max(0.01, speed);

    internal void UseLife() =>
        LifeRemaining--;

    internal void Kill() =>
        IsAlive = false;

    internal void CountStep() =>
        m_stepsSincePheromoneDrop++;

    internal void AdvanceLaunchPhase()
    {
        if (m_launchTicksRemaining > 0)
            m_launchTicksRemaining--;
    }

    internal void Respawn(WorldPoint position, double headingRadians, int maximumLife, double speed)
    {
        Position = position;
        SetState(AntState.Searching);
        HeadingRadians = NormalizeRadians(headingRadians);
        m_desiredHeadingRadians = HeadingRadians;
        m_stepsSincePheromoneDrop = 0;
        m_launchTicksRemaining = Colony.LaunchPhaseTicks;
        ResetLife(maximumLife);
        SetSpeed(speed);
        IsAlive = true;
    }

    internal void Turn(double radians)
    {
        HeadingRadians = NormalizeRadians(HeadingRadians + radians);
        m_desiredHeadingRadians = HeadingRadians;
    }

    public void SetDirection(double directionX, double directionY)
    {
        if (Math.Abs(directionX) < double.Epsilon && Math.Abs(directionY) < double.Epsilon)
            return;

        var heading = NormalizeRadians(Math.Atan2(directionY, directionX));
        HeadingRadians = heading;
        m_desiredHeadingRadians = heading;
    }

    internal void ResetDesiredHeading() =>
        m_desiredHeadingRadians = HeadingRadians;

    internal void SetDesiredDirection(double directionX, double directionY)
    {
        if (Math.Abs(directionX) < double.Epsilon && Math.Abs(directionY) < double.Epsilon)
            return;

        m_desiredHeadingRadians = NormalizeRadians(Math.Atan2(directionY, directionX));
    }

    internal void TurnDesired(double radians) =>
        m_desiredHeadingRadians = NormalizeRadians(m_desiredHeadingRadians + radians);

    internal void RotateTowardDesired(double maximumDeltaRadians)
    {
        var delta = NormalizeRadians(m_desiredHeadingRadians - HeadingRadians);
        delta = Math.Clamp(delta, -maximumDeltaRadians, maximumDeltaRadians);
        HeadingRadians = NormalizeRadians(HeadingRadians + delta);
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
    private const double BucketSize = 24;
    private const float MinimumStrength = 0.03f;
    private readonly List<PheromoneBlob> m_blobs = [];
    private readonly Dictionary<(int X, int Y), List<PheromoneBlob>> m_buckets = [];

    public IReadOnlyList<PheromoneBlob> Blobs => m_blobs;

    public void Add(WorldPoint position, double radius, float strength)
    {
        if (strength < 0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must not be negative.");

        var blob = new PheromoneBlob(position, radius, strength);
        m_blobs.Add(blob);
        AddToBucket(blob);
    }

    public float SampleTotal(WorldPoint position, double radius)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        var total = 0f;
        var radiusSquared = radius * radius;
        var minBucketX = GetBucketCoordinate(position.X - radius);
        var maxBucketX = GetBucketCoordinate(position.X + radius);
        var minBucketY = GetBucketCoordinate(position.Y - radius);
        var maxBucketY = GetBucketCoordinate(position.Y + radius);

        for (var bucketY = minBucketY; bucketY <= maxBucketY; bucketY++)
        for (var bucketX = minBucketX; bucketX <= maxBucketX; bucketX++)
        {
            if (!m_buckets.TryGetValue((bucketX, bucketY), out var bucket))
                continue;

            for (var i = 0; i < bucket.Count; i++)
            {
                var blob = bucket[i];
                if (blob.Position.DistanceSquared(position) <= radiusSquared)
                    total += blob.Strength;
            }
        }

        return total;
    }

    public bool TryFindStrongest(WorldPoint position, double radius, out PheromoneBlob selectedBlob)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        selectedBlob = null;
        var selectedStrength = 0f;
        var selectedDistanceSquared = double.MaxValue;
        var radiusSquared = radius * radius;
        var minBucketX = GetBucketCoordinate(position.X - radius);
        var maxBucketX = GetBucketCoordinate(position.X + radius);
        var minBucketY = GetBucketCoordinate(position.Y - radius);
        var maxBucketY = GetBucketCoordinate(position.Y + radius);

        for (var bucketY = minBucketY; bucketY <= maxBucketY; bucketY++)
        for (var bucketX = minBucketX; bucketX <= maxBucketX; bucketX++)
        {
            if (!m_buckets.TryGetValue((bucketX, bucketY), out var bucket))
                continue;

            for (var i = 0; i < bucket.Count; i++)
            {
                var blob = bucket[i];
                var distanceSquared = blob.Position.DistanceSquared(position);
                if (distanceSquared <= double.Epsilon || distanceSquared > radiusSquared)
                    continue;

                if (selectedBlob != null &&
                    (blob.Strength < selectedStrength ||
                        blob.Strength == selectedStrength && distanceSquared >= selectedDistanceSquared))
                {
                    continue;
                }

                selectedBlob = blob;
                selectedStrength = blob.Strength;
                selectedDistanceSquared = distanceSquared;
            }
        }

        return selectedBlob != null;
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
            {
                m_blobs.RemoveAt(i);
                RemoveFromBucket(blob);
            }
        }
    }

    private void AddToBucket(PheromoneBlob blob)
    {
        var key = GetBucketKey(blob.Position);
        if (!m_buckets.TryGetValue(key, out var bucket))
        {
            bucket = [];
            m_buckets.Add(key, bucket);
        }

        bucket.Add(blob);
    }

    private void RemoveFromBucket(PheromoneBlob blob)
    {
        var key = GetBucketKey(blob.Position);
        if (!m_buckets.TryGetValue(key, out var bucket))
            return;

        bucket.Remove(blob);
        if (bucket.Count == 0)
            m_buckets.Remove(key);
    }

    private static (int X, int Y) GetBucketKey(WorldPoint position) =>
        (GetBucketCoordinate(position.X), GetBucketCoordinate(position.Y));

    private static int GetBucketCoordinate(double value) =>
        (int)Math.Floor(value / BucketSize);
}

/// <summary>
/// Tracks the ants belonging to a single colony and advances their pheromone-led movement.
/// </summary>
public sealed class Colony
{
    /// <summary>
    /// The default maximum random heading change applied when an ant takes a random turn, in radians.
    /// </summary>
    public const double DefaultMaximumRandomTurnRadians = Math.PI / 4;

    /// <summary>
    /// The body radius used to keep live ants from overlapping in world units.
    /// </summary>
    public const double AntRadius = 1.2;

    public const double NestRadius = 20;

    public const int LaunchPhaseTicks = 10;

    /// <summary>
    /// The distance ahead of an ant where left, straight, and right sensor samples are centered.
    /// </summary>
    public const double SensorDistance = 14;

    /// <summary>
    /// The radius of each left, straight, and right sensor sample circle.
    /// </summary>
    public const double SensorRadius = 8;

    /// <summary>
    /// The angular offset between the straight-ahead sensor and the left or right sensor, in radians.
    /// </summary>
    public const double SensorAngleRadians = Math.PI / 4;

    private const double DefaultStepDistance = 1.4;
    private const double PheromoneBlobRadius = 2.5;
    private const double MaximumHeadingTurnRadians = Math.PI / 9;
    private const double AntVariationFraction = 0.4;
    private const double MinimumPheromoneStrengthFraction = 0.15;
    private const double RespawnChancePerTick = 0.06;
    private const int SpawnPositionAttempts = 24;
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
                ant.ResetLife(CreateAntMaximumLife());
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
        ant.ResetDesiredHeading();

        var isLaunching = ant.IsInLaunchPhase;
        var scent = default(ScentSample);
        var food = default(FoodSample);
        var home = default(HomeSample);
        var isFollowingDetectedFood = false;
        var isFollowingDetectedHome = false;
        var isFollowingScent = false;

        if (isLaunching)
        {
            SteerAwayFromNest(ant);
        }
        else
        {
            food = SampleFood(ant);
            home = SampleHome(ant);
            isFollowingDetectedFood = food.HasFood;
            isFollowingDetectedHome = home.HasHome;
            if (isFollowingDetectedFood)
                SteerTowardDesired(ant, food.Position);
            else if (isFollowingDetectedHome)
                SteerTowardDesired(ant, home.Position);
            else
            {
                scent = SampleScent(ant);
                if (TryApplyWeightedScentSteering(ant, scent))
                    isFollowingScent = true;
            }
        }

        if (ant.State == AntState.Returning &&
            ant.Position.DistanceSquared(m_world.NestPosition) <= SensorDistance * SensorDistance * 4)
        {
            SteerTowardDesired(ant, m_world.NestPosition);
            isFollowingDetectedHome = true;
        }

        var randomTurnChance = isLaunching
            ? 0.15
            : isFollowingDetectedFood ||
                isFollowingDetectedHome ||
                isFollowingScent && ant.State == AntState.Returning
                    ? 0
                    : GetScentAdjustedTurnChance(scent);
        if (m_random.NextDouble() < randomTurnChance)
            ant.TurnDesired(isLaunching ? CreateRandomTurnRadians() * 0.35 : CreateRandomTurnRadians());

        ant.RotateTowardDesired(MaximumHeadingTurnRadians);
        MoveForward(ant);
        ant.AdvanceLaunchPhase();

        if (ant.State == AntState.Searching && m_world.TryConsumeFood(ant.Position))
        {
            ant.SetState(AntState.Returning);
            FoodFoundCount++;
            ant.RefreshLife();
            resetLifeThisTick = true;

            if (IsFirstAnt(ant) && !m_hasFirstAntFoundFood)
            {
                m_hasFirstAntFoundFood = true;
                Logger.Instance.Info($"First ant found food at {FormatPoint(ant.Position)}; nest={FormatPoint(m_world.NestPosition)}.");
            }
        }
        else if (ant.State == AntState.Returning && ant.Position.DistanceSquared(m_world.NestPosition) <= Math.Pow(NestRadius + AntRadius, 2))
        {
            if (TryFindSpawnPosition(ant, out var spawnPosition))
            {
                RespawnAntAt(ant, spawnPosition);
                FoodReturnedHomeCount++;
                resetLifeThisTick = true;
            }
            else
            {
                ant.Kill();
                resetLifeThisTick = true;
            }
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

        pheromones.Add(ant.Position, PheromoneBlobRadius, GetPheromoneDepositAmount(ant));
    }

    private float GetPheromoneDepositAmount(Ant ant)
    {
        var lifeFraction = Math.Clamp((double)ant.LifeRemaining / ant.MaximumLife, 0, 1);
        var strengthFraction = MinimumPheromoneStrengthFraction +
            (1 - MinimumPheromoneStrengthFraction) * lifeFraction;
        return (float)(PheromoneDepositAmount * strengthFraction);
    }

    private void MoveForward(Ant ant)
    {
        var movementX = ant.DirectionX;
        var movementY = ant.DirectionY;
        ApplyNeighbourRepulsion(ant, ref movementX, ref movementY);

        var movementLength = Math.Sqrt(movementX * movementX + movementY * movementY);
        if (movementLength <= double.Epsilon)
        {
            ant.Turn(CreateRandomTurnRadians());
            return;
        }

        var next = ant.Position.WithDelta(
            movementX / movementLength * ant.Speed,
            movementY / movementLength * ant.Speed);
        if (!m_world.Contains(next))
        {
            ant.Turn(Math.PI);
            next = ant.Position.WithDelta(ant.DirectionX * ant.Speed, ant.DirectionY * ant.Speed);
        }

        ant.MoveTo(m_world.Clamp(next));
    }

    private void ApplyNeighbourRepulsion(Ant ant, ref double movementX, ref double movementY)
    {
        var separationX = 0.0;
        var separationY = 0.0;
        var interactionRadius = AntRadius * 4;
        var interactionRadiusSquared = interactionRadius * interactionRadius;

        foreach (var other in m_ants)
        {
            if (!other.IsAlive || ReferenceEquals(other, ant))
                continue;

            var deltaX = ant.Position.X - other.Position.X;
            var deltaY = ant.Position.Y - other.Position.Y;
            var distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared > interactionRadiusSquared)
                continue;

            if (distanceSquared <= double.Epsilon)
            {
                var jitter = CreateRandomHeadingRadians();
                deltaX = Math.Cos(jitter);
                deltaY = Math.Sin(jitter);
                distanceSquared = 1;
            }

            var distance = Math.Sqrt(distanceSquared);
            var overlap = Math.Max(0, interactionRadius - distance) / interactionRadius;
            var force = overlap * overlap;
            separationX += deltaX / distance * force;
            separationY += deltaY / distance * force;
        }

        var separationLengthSquared = separationX * separationX + separationY * separationY;
        if (separationLengthSquared <= double.Epsilon)
            return;

        var separationWeight = ant.State == AntState.Returning
            ? 0.65
            : 1.0;
        movementX += separationX * separationWeight;
        movementY += separationY * separationWeight;
    }

    private static double NormalizeRadians(double radians)
    {
        while (radians <= -Math.PI)
            radians += Math.PI * 2;

        while (radians > Math.PI)
            radians -= Math.PI * 2;

        return radians;
    }

    private Ant CreateAnt(WorldPoint position, double headingRadians)
    {
        var ant = new Ant(position, Math.Cos(headingRadians), Math.Sin(headingRadians));
        ant.ResetLife(CreateAntMaximumLife());
        ant.SetSpeed(CreateAntSpeed());
        return ant;
    }

    private void RespawnAntAt(Ant ant, WorldPoint position)
    {
        ant.Respawn(position, CreateSpawnHeadingRadians(position), CreateAntMaximumLife(), CreateAntSpeed());
    }

    private int CreateAntMaximumLife() =>
        Math.Max(1, (int)Math.Round(AntMaximumLife * CreateAntVariationMultiplier()));

    private double CreateAntSpeed() =>
        DefaultStepDistance * CreateAntVariationMultiplier();

    private double CreateAntVariationMultiplier() =>
        1 - AntVariationFraction + m_random.NextDouble() * AntVariationFraction * 2;

    private void AddAntSlotAtNest()
    {
        if (!m_world.HasFoodRemaining || !TryFindSpawnPosition(null, out var spawnPosition))
        {
            var ant = CreateAnt(m_world.NestPosition, CreateSpawnHeadingRadians(m_world.NestPosition));
            ant.Kill();
            m_ants.Add(ant);
            return;
        }

        var spawnedAnt = CreateAnt(spawnPosition, CreateSpawnHeadingRadians(spawnPosition));
        m_ants.Add(spawnedAnt);
    }

    private void RespawnAnt(Ant ant)
    {
        if (m_random.NextDouble() >= RespawnChancePerTick)
        {
            ant.Kill();
            return;
        }

        if (!m_world.HasFoodRemaining || !TryFindSpawnPosition(ant, out var spawnPosition))
        {
            ant.Kill();
            return;
        }

        RespawnAntAt(ant, spawnPosition);
    }

    private bool IsSpawnBlocked(WorldPoint spawnPosition, Ant spawningAnt)
    {
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive || ReferenceEquals(ant, spawningAnt))
                continue;

            if (ant.Position.DistanceSquared(spawnPosition) <= Math.Pow(AntRadius * 2, 2))
                return true;
        }

        return false;
    }

    private bool TryFindSpawnPosition(Ant spawningAnt, out WorldPoint spawnPosition)
    {
        var preferredHeading = CreateSpawnHeadingRadians(m_world.NestPosition);
        for (var i = 0; i < SpawnPositionAttempts; i++)
        {
            var angle = preferredHeading + i * (Math.PI * 2 / SpawnPositionAttempts);
            var candidate = m_world.Clamp(m_world.NestPosition.WithDelta(
                Math.Cos(angle) * NestRadius,
                Math.Sin(angle) * NestRadius));
            var candidateDistanceSquared = candidate.DistanceSquared(m_world.NestPosition);
            if (candidateDistanceSquared < Math.Pow(NestRadius - AntRadius * 0.5, 2) ||
                candidateDistanceSquared > Math.Pow(NestRadius + AntRadius * 0.5, 2))
            {
                continue;
            }
            if (IsSpawnBlocked(candidate, spawningAnt))
                continue;

            spawnPosition = candidate;
            return true;
        }

        spawnPosition = WorldPoint.Zero;
        return false;
    }

    private static bool TryApplyWeightedScentSteering(Ant ant, ScentSample scent)
    {
        var leftHeading = ant.HeadingRadians - SensorAngleRadians;
        var straightHeading = ant.HeadingRadians;
        var rightHeading = ant.HeadingRadians + SensorAngleRadians;

        if (scent.LeftStrength <= 0 && scent.StraightStrength <= 0 && scent.RightStrength <= 0)
            return false;

        var totalX = 0.0;
        var totalY = 0.0;
        AddWeightedHeadingContribution(leftHeading, scent.LeftStrength, ref totalX, ref totalY);
        AddWeightedHeadingContribution(straightHeading, scent.StraightStrength, ref totalX, ref totalY);
        AddWeightedHeadingContribution(rightHeading, scent.RightStrength, ref totalX, ref totalY);
        if (Math.Abs(totalX) < double.Epsilon && Math.Abs(totalY) < double.Epsilon)
            return false;

        ant.SetDesiredDirection(totalX, totalY);
        return true;
    }

    private static void AddWeightedHeadingContribution(double heading, float strength, ref double totalX, ref double totalY)
    {
        if (strength <= 0)
            return;

        totalX += Math.Cos(heading) * strength;
        totalY += Math.Sin(heading) * strength;
    }

    private static void SteerToward(Ant ant, WorldPoint target)
    {
        ant.SetDirection(target.X - ant.Position.X, target.Y - ant.Position.Y);
    }

    private static void SteerTowardDesired(Ant ant, WorldPoint target)
    {
        ant.SetDesiredDirection(target.X - ant.Position.X, target.Y - ant.Position.Y);
    }

    private void SteerAwayFromNest(Ant ant)
    {
        var awayX = ant.Position.X - m_world.NestPosition.X;
        var awayY = ant.Position.Y - m_world.NestPosition.Y;
        if (Math.Abs(awayX) < double.Epsilon && Math.Abs(awayY) < double.Epsilon)
        {
            var heading = CreateSpawnHeadingRadians(m_world.NestPosition);
            ant.SetDesiredDirection(Math.Cos(heading), Math.Sin(heading));
            return;
        }

        ant.SetDesiredDirection(awayX, awayY);
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
        var detectionRadius = SensorRadius + NestRadius;
        return m_world.NestPosition.DistanceSquared(samplePosition) <= detectionRadius * detectionRadius;
    }

    private static ScentSample SampleScent(Ant ant, PheromoneField pheromones)
    {
        var leftStrength = SampleAhead(ant, pheromones, -SensorAngleRadians);
        var straightStrength = SampleAhead(ant, pheromones, 0);
        var rightStrength = SampleAhead(ant, pheromones, SensorAngleRadians);
        var selectedStrength = 0f;
        var hasSignal = false;

        ConsiderScentSample(straightStrength, ref selectedStrength, ref hasSignal);
        ConsiderScentSample(leftStrength, ref selectedStrength, ref hasSignal);
        ConsiderScentSample(rightStrength, ref selectedStrength, ref hasSignal);

        return new ScentSample(selectedStrength, hasSignal, leftStrength, straightStrength, rightStrength);
    }

    private static void ConsiderScentSample(
        float strength,
        ref float selectedStrength,
        ref bool hasSignal)
    {
        if (strength <= 0)
            return;
        if (hasSignal && strength <= selectedStrength)
            return;
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
        var detectionRadius = SensorDistance + SensorRadius + PheromoneBlobRadius;
        return pheromones.TryFindStrongest(position, detectionRadius, out var blob)
            ? new PheromoneTarget(blob.Position, true)
            : new PheromoneTarget(WorldPoint.Zero, false);
    }

    private readonly record struct ScentSample(
        float SelectedStrength,
        bool HasSignal,
        float LeftStrength,
        float StraightStrength,
        float RightStrength);

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

    /// <summary>
    /// The number of food blobs generated around each food source vicinity.
    /// </summary>
    public const int FoodBlobsPerSource = 40;

    /// <summary>
    /// The number of ants each individual food blob can feed before it is depleted.
    /// </summary>
    public const int FoodAmountPerBlob = 10;

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

        var random = new Random(randomSeed);
        Width = width;
        Height = height;
        NestPosition = CreateNestPosition(random);
        HomePheromones = new PheromoneField();
        FoodPheromones = new PheromoneField();
        FoodSources = CreateFoodSources(foodSourceCount, random);
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

    public int FoodRemaining
    {
        get
        {
            var result = 0;
            foreach (var source in FoodSources)
                result += source.RemainingAmount;

            return result;
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

    private WorldPoint CreateNestPosition(Random random)
    {
        var horizontalMargin = Math.Min(Colony.NestRadius, Math.Max(0, (Width - 1) / 2.0));
        var verticalMargin = Math.Min(Colony.NestRadius, Math.Max(0, (Height - 1) / 2.0));
        var minX = horizontalMargin;
        var minY = verticalMargin;
        var maxX = Math.Max(minX, Width - 1 - horizontalMargin);
        var maxY = Math.Max(minY, Height - 1 - verticalMargin);
        return new WorldPoint(
            random.NextDouble() * (maxX - minX) + minX,
            random.NextDouble() * (maxY - minY) + minY);
    }

    private IReadOnlyList<FoodSource> CreateFoodSources(int foodSourceCount, Random random)
    {
        var sources = new List<FoodSource>(foodSourceCount * FoodBlobsPerSource);
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
            sources.Add(new FoodSource(position, blobRadius, FoodAmountPerBlob));
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
