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

    public WorldPoint Position { get; set; }

    public double HeadingRadians { get; private set; }

    public double DirectionX => Math.Cos(HeadingRadians);

    public double DirectionY => Math.Sin(HeadingRadians);

    public AntState State { get; set; }

    public bool IsAlive { get; private set; } = true;

    public int MaximumLife { get; private set; }

    public int LifeRemaining { get; private set; }

    public double Speed { get; private set; } = 1;

    public bool IsInLaunchPhase => m_launchTicksRemaining > 0;

    public bool ShouldDropPheromone(int dropInterval) =>
        m_stepsSincePheromoneDrop % Math.Max(1, dropInterval) == 0;

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
        State = AntState.Searching;
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

    private void SetDirection(double directionX, double directionY)
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

    private static double NormalizeRadians(double radians)
    {
        while (radians <= -Math.PI)
            radians += Math.PI * 2;

        while (radians > Math.PI)
            radians -= Math.PI * 2;

        return radians;
    }
}
