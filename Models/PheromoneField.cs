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
        if (m_blobs.Count == 0)
            return 0;

        var total = 0f;
        var sampleX = position.X;
        var sampleY = position.Y;
        var radiusSquared = radius * radius;
        var minBucketX = GetBucketCoordinate(sampleX - radius);
        var maxBucketX = GetBucketCoordinate(sampleX + radius);
        var minBucketY = GetBucketCoordinate(sampleY - radius);
        var maxBucketY = GetBucketCoordinate(sampleY + radius);

        for (var bucketY = minBucketY; bucketY <= maxBucketY; bucketY++)
        for (var bucketX = minBucketX; bucketX <= maxBucketX; bucketX++)
        {
            if (!m_buckets.TryGetValue((bucketX, bucketY), out var bucket))
                continue;

            for (var i = 0; i < bucket.Count; i++)
            {
                var blob = bucket[i];
                var blobPosition = blob.Position;
                var deltaX = blobPosition.X - sampleX;
                var deltaY = blobPosition.Y - sampleY;
                if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                    total += blob.Strength;
            }
        }

        return total;
    }

    public bool TryFindStrongest(WorldPoint position, double radius, out PheromoneBlob selectedBlob)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        if (m_blobs.Count == 0)
        {
            selectedBlob = null;
            return false;
        }

        selectedBlob = null;
        var selectedStrength = 0f;
        var selectedDistanceSquared = double.MaxValue;
        var sampleX = position.X;
        var sampleY = position.Y;
        var radiusSquared = radius * radius;
        var minBucketX = GetBucketCoordinate(sampleX - radius);
        var maxBucketX = GetBucketCoordinate(sampleX + radius);
        var minBucketY = GetBucketCoordinate(sampleY - radius);
        var maxBucketY = GetBucketCoordinate(sampleY + radius);

        for (var bucketY = minBucketY; bucketY <= maxBucketY; bucketY++)
        for (var bucketX = minBucketX; bucketX <= maxBucketX; bucketX++)
        {
            if (!m_buckets.TryGetValue((bucketX, bucketY), out var bucket))
                continue;

            for (var i = 0; i < bucket.Count; i++)
            {
                var blob = bucket[i];
                var blobPosition = blob.Position;
                var deltaX = blobPosition.X - sampleX;
                var deltaY = blobPosition.Y - sampleY;
                var distanceSquared = deltaX * deltaX + deltaY * deltaY;
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
