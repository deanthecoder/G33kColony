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