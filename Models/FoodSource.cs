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
/// Represents a finite circular food blob that searching ants can consume.
/// </summary>
public sealed class FoodSource
{
    private const double DetectionPadding = 3.5;
    private readonly double m_effectiveRadiusSquared;

    public FoodSource(WorldPoint position, double radius, int amount)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");

        Position = position;
        Radius = radius;
        EffectiveRadius = Radius + DetectionPadding;
        m_effectiveRadiusSquared = EffectiveRadius * EffectiveRadius;
        InitialAmount = amount;
        RemainingAmount = amount;
    }

    public WorldPoint Position { get; }

    public double Radius { get; }

    public double EffectiveRadius { get; }

    public int InitialAmount { get; }

    public int RemainingAmount { get; private set; }

    public bool IsDepleted => RemainingAmount <= 0;

    public bool Contains(WorldPoint point) =>
        !IsDepleted && Position.DistanceSquared(point) <= m_effectiveRadiusSquared;

    public bool TryConsume(WorldPoint point)
    {
        if (!Contains(point))
            return false;

        RemainingAmount--;
        return true;
    }
}
