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
using System.Threading.Tasks;
using DTC.Core;

namespace G33kColony.Models;

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
    private const int PheromoneDropInterval = 8;
    private const double AntInteractionRadius = AntRadius * 4;
    private const double AntBucketSize = AntInteractionRadius;
    private readonly List<Ant> m_ants;
    private readonly Dictionary<(int X, int Y), List<Ant>> m_antBuckets = [];
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

        RebuildAntBuckets();
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
        RebuildAntBuckets();
        var precomputedScents = PrecomputeScentSamples();

        for (var i = 0; i < m_ants.Count; i++)
        {
            var ant = m_ants[i];
            if (!ant.IsAlive)
            {
                RespawnAnt(ant);
                continue;
            }

            Wander(ant, precomputedScents[i], true);
        }
    }

    private void Wander(Ant ant, ScentSample precomputedScent, bool hasPrecomputedScent)
    {
        var resetLifeThisTick = false;

        if (ant.ShouldDropPheromone(PheromoneDropInterval))
            DepositPheromone(ant);
        ant.ResetDesiredHeading();

        var isLaunching = ant.IsInLaunchPhase;
        var scent = default(ScentSample);
        var isFollowingDetectedFood = false;
        var isFollowingDetectedHome = false;
        var isFollowingScent = false;

        if (isLaunching)
        {
            SteerAwayFromNest(ant);
        }
        else
        {
            var food = SampleFood(ant);
            var home = SampleHome(ant);
            isFollowingDetectedFood = food.HasFood;
            isFollowingDetectedHome = home.HasHome;
            if (isFollowingDetectedFood)
                SteerTowardDesired(ant, food.Position);
            else if (isFollowingDetectedHome)
            {
                SteerTowardDesired(ant, home.Position);
            }
            else
            {
                scent = hasPrecomputedScent
                    ? precomputedScent
                    : SampleScent(ant);
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
            ant.State = AntState.Returning;
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

    private ScentSample[] PrecomputeScentSamples()
    {
        var precomputed = new ScentSample[m_ants.Count];
        if (m_ants.Count < 32)
        {
            for (var i = 0; i < m_ants.Count; i++)
            {
                var ant = m_ants[i];
                if (ant.IsAlive && !ant.IsInLaunchPhase)
                    precomputed[i] = SampleScent(ant);
            }

            return precomputed;
        }

        Parallel.For(0, m_ants.Count, i =>
        {
            var ant = m_ants[i];
            if (ant.IsAlive && !ant.IsInLaunchPhase)
                precomputed[i] = SampleScent(ant);
        });

        return precomputed;
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
        var currentPosition = ant.Position;
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
            next = currentPosition.WithDelta(ant.DirectionX * ant.Speed, ant.DirectionY * ant.Speed);
        }

        if (m_world.IsObstacle(next))
        {
            if (!TryFindUnblockedStep(ant, currentPosition, out next))
            {
                ant.Turn(CreateRandomTurnRadians());
                return;
            }
        }

        ant.Position = m_world.Clamp(next);
    }

    private bool TryFindUnblockedStep(Ant ant, WorldPoint currentPosition, out WorldPoint next)
    {
        var candidateOffsets = new[]
        {
            Math.PI / 8,
            -Math.PI / 8,
            Math.PI / 4,
            -Math.PI / 4,
            Math.PI / 2,
            -Math.PI / 2,
            Math.PI
        };

        for (var i = 0; i < candidateOffsets.Length; i++)
        {
            var offset = candidateOffsets[i];
            var heading = ant.HeadingRadians + offset;
            var candidate = currentPosition.WithDelta(
                Math.Cos(heading) * ant.Speed,
                Math.Sin(heading) * ant.Speed);
            if (!m_world.Contains(candidate) || m_world.IsObstacle(candidate))
                continue;

            ant.Turn(offset);
            next = candidate;
            return true;
        }

        next = currentPosition;
        return false;
    }

    private void ApplyNeighbourRepulsion(Ant ant, ref double movementX, ref double movementY)
    {
        var separationX = 0.0;
        var separationY = 0.0;
        var antPosition = ant.Position;
        var antX = antPosition.X;
        var antY = antPosition.Y;
        const double interactionRadiusSquared = AntInteractionRadius * AntInteractionRadius;
        var antBucket = GetAntBucketKey(antPosition);
        var bucketRange = (int)Math.Ceiling(AntInteractionRadius / AntBucketSize);

        for (var bucketY = antBucket.Y - bucketRange; bucketY <= antBucket.Y + bucketRange; bucketY++)
        {
            for (var bucketX = antBucket.X - bucketRange; bucketX <= antBucket.X + bucketRange; bucketX++)
            {
                if (!m_antBuckets.TryGetValue((bucketX, bucketY), out var bucket))
                    continue;

                for (var i = 0; i < bucket.Count; i++)
                {
                    var other = bucket[i];
                    if (!other.IsAlive || ReferenceEquals(other, ant))
                        continue;

                    var otherPosition = other.Position;
                    var deltaX = antX - otherPosition.X;
                    var deltaY = antY - otherPosition.Y;
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
                    var overlap = Math.Max(0, AntInteractionRadius - distance) / AntInteractionRadius;
                    var force = overlap * overlap;
                    separationX += deltaX / distance * force;
                    separationY += deltaY / distance * force;
                }
            }
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
        m_world.ConsiderNearbyFood(
            samplePosition,
            ant.Position,
            SensorRadius,
            ref selectedPosition,
            ref selectedDistanceSquared,
            ref hasFood);
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
        const double detectionRadius = SensorRadius + NestRadius;
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

    private void RebuildAntBuckets()
    {
        m_antBuckets.Clear();
        foreach (var ant in m_ants)
        {
            if (!ant.IsAlive)
                continue;

            AddAntToBucket(ant);
        }
    }

    private void AddAntToBucket(Ant ant)
    {
        if (!ant.IsAlive)
            return;

        var key = GetAntBucketKey(ant.Position);
        if (!m_antBuckets.TryGetValue(key, out var bucket))
        {
            bucket = [];
            m_antBuckets[key] = bucket;
        }

        bucket.Add(ant);
    }

    private static (int X, int Y) GetAntBucketKey(WorldPoint position) =>
        ((int)Math.Floor(position.X / AntBucketSize), (int)Math.Floor(position.Y / AntBucketSize));

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
        const double detectionRadius = SensorDistance + SensorRadius + PheromoneBlobRadius;
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
