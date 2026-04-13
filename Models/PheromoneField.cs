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
/// Stores pheromone intensity in a fixed-size scalar grid for fast updates and sampling.
/// </summary>
public sealed class PheromoneField
{
    private const int MinimumAxisResolution = 200;
    private const float MinimumStrength = 0.03f;
    private readonly float[] m_grid;
    private readonly int m_gridWidth;
    private readonly int m_gridHeight;
    private readonly double m_cellSize;

    public PheromoneField()
        : this(World.DefaultWidth, World.DefaultHeight)
    {
    }

    public PheromoneField(int worldWidth, int worldHeight)
    {
        if (worldWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldWidth), "World width must be greater than zero.");
        if (worldHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldHeight), "World height must be greater than zero.");

        var minimumAxis = Math.Min(worldWidth, worldHeight);
        m_cellSize = Math.Max(1.0, minimumAxis / (double)MinimumAxisResolution);
        m_gridWidth = Math.Max(1, (int)Math.Ceiling(worldWidth / m_cellSize));
        m_gridHeight = Math.Max(1, (int)Math.Ceiling(worldHeight / m_cellSize));
        m_grid = new float[m_gridWidth * m_gridHeight];
    }

    public int GridWidth => m_gridWidth;

    public int GridHeight => m_gridHeight;

    public double CellSize => m_cellSize;

    public void Add(WorldPoint position, double radius, float strength)
    {
        if (strength < 0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must not be negative.");
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        if (strength <= 0)
            return;

        var centerX = ToGridX(position.X);
        var centerY = ToGridY(position.Y);
        m_grid[centerY * m_gridWidth + centerX] += strength;
    }

    public float SampleTotal(WorldPoint position, double radius)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        var centerX = ToGridX(position.X);
        var centerY = ToGridY(position.Y);
        var radiusInCells = Math.Max(1, (int)Math.Ceiling(radius / m_cellSize));
        var radiusSquared = radius * radius;
        var minX = Math.Max(0, centerX - radiusInCells);
        var maxX = Math.Min(m_gridWidth - 1, centerX + radiusInCells);
        var minY = Math.Max(0, centerY - radiusInCells);
        var maxY = Math.Min(m_gridHeight - 1, centerY + radiusInCells);
        var total = 0f;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var value = m_grid[y * m_gridWidth + x];
            if (value < MinimumStrength)
                continue;

            var cellCenter = GetCellCenter(x, y);
            var deltaX = cellCenter.X - position.X;
            var deltaY = cellCenter.Y - position.Y;
            if (deltaX * deltaX + deltaY * deltaY > radiusSquared)
                continue;

            total += value;
        }

        return total;
    }

    public bool TryFindStrongestPosition(WorldPoint position, double radius, out WorldPoint strongestPosition)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");

        var centerX = ToGridX(position.X);
        var centerY = ToGridY(position.Y);
        var radiusInCells = Math.Max(1, (int)Math.Ceiling(radius / m_cellSize));
        var radiusSquared = radius * radius;
        var minX = Math.Max(0, centerX - radiusInCells);
        var maxX = Math.Min(m_gridWidth - 1, centerX + radiusInCells);
        var minY = Math.Max(0, centerY - radiusInCells);
        var maxY = Math.Min(m_gridHeight - 1, centerY + radiusInCells);
        var bestStrength = 0f;
        var bestDistanceSquared = double.MaxValue;
        var found = false;
        var bestX = 0;
        var bestY = 0;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var value = m_grid[y * m_gridWidth + x];
            if (value < MinimumStrength)
                continue;

            var cellCenter = GetCellCenter(x, y);
            var deltaX = cellCenter.X - position.X;
            var deltaY = cellCenter.Y - position.Y;
            var distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared <= double.Epsilon || distanceSquared > radiusSquared)
                continue;

            if (found && (value < bestStrength || value == bestStrength && distanceSquared >= bestDistanceSquared))
                continue;

            found = true;
            bestStrength = value;
            bestDistanceSquared = distanceSquared;
            bestX = x;
            bestY = y;
        }

        if (!found)
        {
            strongestPosition = WorldPoint.Zero;
            return false;
        }

        strongestPosition = GetCellCenter(bestX, bestY);
        return true;
    }

    public float GetCellStrength(int x, int y)
    {
        if (x < 0 || y < 0 || x >= m_gridWidth || y >= m_gridHeight)
            return 0;

        return m_grid[y * m_gridWidth + x];
    }

    public WorldPoint GetCellCenter(int x, int y)
    {
        var centerX = (x + 0.5) * m_cellSize;
        var centerY = (y + 0.5) * m_cellSize;
        return new WorldPoint(centerX, centerY);
    }

    public void Evaporate(float retention)
    {
        if (retention is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be between 0 and 1.");

        for (var i = 0; i < m_grid.Length; i++)
        {
            var value = m_grid[i] * retention;
            m_grid[i] = value < MinimumStrength ? 0 : value;
        }
    }

    private int ToGridX(double worldX) =>
        Math.Clamp((int)(worldX / m_cellSize), 0, m_gridWidth - 1);

    private int ToGridY(double worldY) =>
        Math.Clamp((int)(worldY / m_cellSize), 0, m_gridHeight - 1);
}
