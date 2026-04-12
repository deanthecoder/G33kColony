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
