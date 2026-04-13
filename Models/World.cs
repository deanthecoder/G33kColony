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

namespace G33kColony.Models;

/// <summary>
/// Contains fixed-size simulation data for the ant colony world.
/// </summary>
public sealed class World
{
    public const int DefaultWidth = 640;
    public const int DefaultHeight = 480;
    private const double FoodBucketSize = 16;
    private readonly Dictionary<(int X, int Y), List<FoodSource>> m_foodBuckets;
    private readonly double m_maxFoodEffectiveRadius;
    private readonly bool[] m_obstacles;
    private int m_obstacleCount;

    /// <summary>
    /// The number of food blobs generated around each food source vicinity.
    /// </summary>
    public const int FoodBlobsPerSource = 40;

    /// <summary>
    /// The number of ants each individual food blob can feed before it is depleted.
    /// </summary>
    private const int FoodAmountPerBlob = 100;

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
        HomePheromones = new PheromoneField(width, height);
        FoodPheromones = new PheromoneField(width, height);
        FoodSources = CreateFoodSources(foodSourceCount, random);
        m_foodBuckets = BuildFoodBuckets(FoodSources);
        m_maxFoodEffectiveRadius = GetMaximumFoodEffectiveRadius(FoodSources);
        m_obstacles = new bool[Width * Height];
    }

    public int Width { get; }

    public int Height { get; }

    public WorldPoint NestPosition { get; }

    public PheromoneField HomePheromones { get; }

    public PheromoneField FoodPheromones { get; }

    public IReadOnlyList<FoodSource> FoodSources { get; }

    public bool HasObstacles => m_obstacleCount > 0;

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
        GetFoodBucketBounds(position, 0, out var minX, out var maxX, out var minY, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!m_foodBuckets.TryGetValue((x, y), out var bucket))
                    continue;

                for (var i = 0; i < bucket.Count; i++)
                {
                    if (bucket[i].Contains(position))
                        return true;
                }
            }
        }

        return false;
    }

    public bool TryConsumeFood(WorldPoint position)
    {
        GetFoodBucketBounds(position, 0, out var minX, out var maxX, out var minY, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!m_foodBuckets.TryGetValue((x, y), out var bucket))
                    continue;

                for (var i = 0; i < bucket.Count; i++)
                {
                    if (bucket[i].TryConsume(position))
                        return true;
                }
            }
        }

        return false;
    }

    internal void ConsiderNearbyFood(
        WorldPoint samplePosition,
        WorldPoint antPosition,
        double sampleRadius,
        ref WorldPoint selectedPosition,
        ref double selectedDistanceSquared,
        ref bool hasFood)
    {
        GetFoodBucketBounds(samplePosition, sampleRadius, out var minX, out var maxX, out var minY, out var maxY);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!m_foodBuckets.TryGetValue((x, y), out var bucket))
                    continue;

                for (var i = 0; i < bucket.Count; i++)
                {
                    var source = bucket[i];
                    if (source.IsDepleted)
                        continue;

                    var detectionRadius = sampleRadius + source.Radius;
                    if (source.Position.DistanceSquared(samplePosition) > detectionRadius * detectionRadius)
                        continue;

                    var distanceSquared = source.Position.DistanceSquared(antPosition);
                    if (hasFood && distanceSquared >= selectedDistanceSquared)
                        continue;

                    selectedPosition = source.Position;
                    selectedDistanceSquared = distanceSquared;
                    hasFood = true;
                }
            }
        }
    }

    public bool Contains(WorldPoint position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public WorldPoint Clamp(WorldPoint position) =>
        new(Math.Clamp(position.X, 0, Width - 1), Math.Clamp(position.Y, 0, Height - 1));

    public bool IsObstacle(WorldPoint position)
    {
        var x = (int)Math.Round(position.X);
        var y = (int)Math.Round(position.Y);
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return true;

        return m_obstacles[y * Width + x];
    }

    public bool IsObstacleCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return true;

        return m_obstacles[y * Width + x];
    }

    public void SetObstacleCircle(WorldPoint center, double radius, bool isObstacle)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        var minX = Math.Max(0, (int)Math.Floor(center.X - radius));
        var maxX = Math.Min(Width - 1, (int)Math.Ceiling(center.X + radius));
        var minY = Math.Max(0, (int)Math.Floor(center.Y - radius));
        var maxY = Math.Min(Height - 1, (int)Math.Ceiling(center.Y + radius));
        var radiusSquared = radius * radius;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var deltaX = x - center.X;
            var deltaY = y - center.Y;
            if (deltaX * deltaX + deltaY * deltaY > radiusSquared)
                continue;

            var index = y * Width + x;
            var wasObstacle = m_obstacles[index];
            if (wasObstacle == isObstacle)
                continue;

            m_obstacles[index] = isObstacle;
            m_obstacleCount += isObstacle ? 1 : -1;
        }
    }

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

    private void GetFoodBucketBounds(
        WorldPoint position,
        double sampleRadius,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY)
    {
        var lookupRadius = sampleRadius + m_maxFoodEffectiveRadius;
        minX = ToBucketIndex(position.X - lookupRadius);
        maxX = ToBucketIndex(position.X + lookupRadius);
        minY = ToBucketIndex(position.Y - lookupRadius);
        maxY = ToBucketIndex(position.Y + lookupRadius);
    }

    private Dictionary<(int X, int Y), List<FoodSource>> BuildFoodBuckets(IReadOnlyList<FoodSource> foodSources)
    {
        var buckets = new Dictionary<(int X, int Y), List<FoodSource>>();
        foreach (var source in foodSources)
        {
            var key = (ToBucketIndex(source.Position.X), ToBucketIndex(source.Position.Y));
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = [];
                buckets[key] = bucket;
            }

            bucket.Add(source);
        }

        return buckets;
    }

    private static double GetMaximumFoodEffectiveRadius(IReadOnlyList<FoodSource> foodSources)
    {
        var maxRadius = 0.0;
        foreach (var source in foodSources)
            maxRadius = Math.Max(maxRadius, source.EffectiveRadius);

        return maxRadius;
    }

    private static int ToBucketIndex(double coordinate) =>
        (int)Math.Floor(coordinate / FoodBucketSize);
}
