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

namespace G33kColony.Models;

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
