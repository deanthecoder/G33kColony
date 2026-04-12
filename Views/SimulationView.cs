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
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G33kColony.Models;

namespace G33kColony.Views;

/// <summary>
/// Renders the continuous simulation world to a bitmap that scales uniformly to fit the view.
/// </summary>
public sealed class SimulationView : Control
{
    public static readonly StyledProperty<World> WorldProperty =
        AvaloniaProperty.Register<SimulationView, World>(nameof(World));

    public static readonly StyledProperty<Colony> ColonyProperty =
        AvaloniaProperty.Register<SimulationView, Colony>(nameof(Colony));

    public static readonly StyledProperty<bool> ShowHomePheromonesProperty =
        AvaloniaProperty.Register<SimulationView, bool>(nameof(ShowHomePheromones), true);

    public static readonly StyledProperty<bool> ShowFoodPheromonesProperty =
        AvaloniaProperty.Register<SimulationView, bool>(nameof(ShowFoodPheromones), true);

    public static readonly StyledProperty<bool> ShowSensorOverlayProperty =
        AvaloniaProperty.Register<SimulationView, bool>(nameof(ShowSensorOverlay));

    public static readonly StyledProperty<int> FrameNumberProperty =
        AvaloniaProperty.Register<SimulationView, int>(nameof(FrameNumber));

    private WriteableBitmap m_bitmap;
    private byte[] m_pixels;

    public World World
    {
        get => GetValue(WorldProperty);
        set => SetValue(WorldProperty, value);
    }

    public Colony Colony
    {
        get => GetValue(ColonyProperty);
        set => SetValue(ColonyProperty, value);
    }

    public bool ShowHomePheromones
    {
        get => GetValue(ShowHomePheromonesProperty);
        set => SetValue(ShowHomePheromonesProperty, value);
    }

    public bool ShowFoodPheromones
    {
        get => GetValue(ShowFoodPheromonesProperty);
        set => SetValue(ShowFoodPheromonesProperty, value);
    }

    public bool ShowSensorOverlay
    {
        get => GetValue(ShowSensorOverlayProperty);
        set => SetValue(ShowSensorOverlayProperty, value);
    }

    public int FrameNumber
    {
        get => GetValue(FrameNumberProperty);
        set => SetValue(FrameNumberProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WorldProperty)
            ResetBitmap();

        if (change.Property == WorldProperty ||
            change.Property == ColonyProperty ||
            change.Property == ShowHomePheromonesProperty ||
            change.Property == ShowFoodPheromonesProperty ||
            change.Property == ShowSensorOverlayProperty ||
            change.Property == FrameNumberProperty)
        {
            RenderWorld();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (World == null)
            return;

        EnsureBitmap();
        if (m_bitmap == null)
            return;

        var destination = GetUniformDestinationRect(World.Width, World.Height);
        context.DrawImage(
            m_bitmap,
            new Rect(0, 0, World.Width, World.Height),
            destination);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResetBitmap();
        base.OnDetachedFromVisualTree(e);
    }

    private void RenderWorld()
    {
        if (World == null)
            return;

        EnsureBitmap();
        if (m_bitmap == null)
            return;

        DrawWorldPixels();

        using var framebuffer = m_bitmap.Lock();
        var sourceRowBytes = World.Width * 4;
        if (framebuffer.RowBytes == sourceRowBytes)
        {
            Marshal.Copy(m_pixels, 0, framebuffer.Address, m_pixels.Length);
            return;
        }

        for (var y = 0; y < World.Height; y++)
        {
            var sourceOffset = y * sourceRowBytes;
            var target = IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes);
            Marshal.Copy(m_pixels, sourceOffset, target, sourceRowBytes);
        }
    }

    private void EnsureBitmap()
    {
        if (World == null)
            return;

        if (m_bitmap != null &&
            m_bitmap.PixelSize.Width == World.Width &&
            m_bitmap.PixelSize.Height == World.Height)
        {
            return;
        }

        ResetBitmap();
        m_pixels = new byte[World.Width * World.Height * 4];
        m_bitmap = new WriteableBitmap(
            new PixelSize(World.Width, World.Height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque);
    }

    private void ResetBitmap()
    {
        m_bitmap?.Dispose();
        m_bitmap = null;
        m_pixels = null;
    }

    private void DrawWorldPixels()
    {
        for (var i = 0; i < World.Width * World.Height; i++)
            SetPixel(i, 18, 18, 18);

        if (ShowHomePheromones)
            DrawPheromones(World.HomePheromones, 0, 1.0, 1.0);

        if (ShowFoodPheromones)
            DrawPheromones(World.FoodPheromones, 1.0, 0.25, 0);

        DrawFoodSources();
        DrawNest();
        DrawSensorOverlay();
        DrawAnts();
    }

    private void DrawPheromones(PheromoneField pheromones, double redScale, double greenScale, double blueScale)
    {
        foreach (var blob in pheromones.Blobs)
        {
            var intensity = ScalePheromone(blob.Strength);
            AddCircle(
                blob.Position,
                blob.Radius,
                (int)(intensity * redScale),
                (int)(intensity * greenScale),
                (int)(intensity * blueScale));
        }
    }

    private void DrawFoodSources()
    {
        foreach (var source in World.FoodSources)
        {
            if (source.IsDepleted)
                continue;

            var ratio = source.InitialAmount == 0 ? 0 : (double)source.RemainingAmount / source.InitialAmount;
            AddCircle(source.Position, source.Radius, (int)(90 + 40 * ratio), (int)(150 + 80 * ratio), 70);
        }
    }

    private void DrawNest()
    {
        var nest = World.NestPosition;
        AddCircle(nest, Colony.NestArrivalRadius * 2.8, 40, 120, 210);
        AddCircle(nest, Colony.NestArrivalRadius * 1.7, 230, 245, 255);
        AddCircle(nest, Colony.NestArrivalRadius, 255, 255, 255);
        AddCircleOutline(nest, Colony.NestArrivalRadius * 2.8, 80, 190, 255);
        AddCircleOutline(nest, Colony.NestArrivalRadius * 1.7, 255, 255, 255);
    }

    private void DrawAnts()
    {
        if (Colony == null)
            return;

        foreach (var ant in Colony.Ants)
        {
            if (!ant.IsAlive)
                continue;

            var x = (int)Math.Round(ant.Position.X);
            var y = (int)Math.Round(ant.Position.Y);
            if (x < 0 || y < 0 || x >= World.Width || y >= World.Height)
                continue;

            var index = y * World.Width + x;
            if (ant.State == AntState.Returning)
                SetPixel(index, 235, 55, 45);
            else
                SetPixel(index, 245, 245, 230);
        }
    }

    private void DrawSensorOverlay()
    {
        if (!ShowSensorOverlay || Colony == null)
            return;

        foreach (var ant in Colony.Ants)
        {
            if (!ant.IsAlive)
                continue;

            DrawSensorArea(ant, -Colony.SensorAngleRadians, 40, 120, 255);
            DrawSensorArea(ant, 0, 245, 245, 245);
            DrawSensorArea(ant, Colony.SensorAngleRadians, 255, 210, 50);
        }
    }

    private void DrawSensorArea(Ant ant, double angleOffset, int red, int green, int blue)
    {
        var heading = ant.HeadingRadians + angleOffset;
        var center = ant.Position.WithDelta(
            Math.Cos(heading) * Colony.SensorDistance,
            Math.Sin(heading) * Colony.SensorDistance);

        AddCircle(center, Colony.SensorRadius, red / 6, green / 6, blue / 6);
        AddCircleOutline(center, Colony.SensorRadius, red, green, blue);
    }

    private Rect GetUniformDestinationRect(double sourceWidth, double sourceHeight)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return new Rect();

        var scale = Math.Min(Bounds.Width / sourceWidth, Bounds.Height / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var x = (Bounds.Width - width) / 2;
        var y = (Bounds.Height - height) / 2;
        return new Rect(x, y, width, height);
    }

    private void SetPixel(int pixelIndex, int red, int green, int blue)
    {
        var byteIndex = pixelIndex * 4;
        m_pixels[byteIndex] = (byte)blue;
        m_pixels[byteIndex + 1] = (byte)green;
        m_pixels[byteIndex + 2] = (byte)red;
        m_pixels[byteIndex + 3] = 255;
    }

    private void AddCircle(WorldPoint center, double radius, int red, int green, int blue)
    {
        var minX = Math.Max(0, (int)Math.Floor(center.X - radius));
        var maxX = Math.Min(World.Width - 1, (int)Math.Ceiling(center.X + radius));
        var minY = Math.Max(0, (int)Math.Floor(center.Y - radius));
        var maxY = Math.Min(World.Height - 1, (int)Math.Ceiling(center.Y + radius));
        var radiusSquared = radius * radius;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var deltaX = x - center.X;
            var deltaY = y - center.Y;
            var distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared > radiusSquared)
                continue;

            var falloff = 1 - Math.Sqrt(distanceSquared) / radius;
            AddPixel(y * World.Width + x, (int)(red * falloff), (int)(green * falloff), (int)(blue * falloff));
        }
    }

    private void AddCircleOutline(WorldPoint center, double radius, int red, int green, int blue)
    {
        var minX = Math.Max(0, (int)Math.Floor(center.X - radius - 1));
        var maxX = Math.Min(World.Width - 1, (int)Math.Ceiling(center.X + radius + 1));
        var minY = Math.Max(0, (int)Math.Floor(center.Y - radius - 1));
        var maxY = Math.Min(World.Height - 1, (int)Math.Ceiling(center.Y + radius + 1));
        var innerRadius = Math.Max(0, radius - 0.75);
        var outerRadius = radius + 0.75;
        var innerRadiusSquared = innerRadius * innerRadius;
        var outerRadiusSquared = outerRadius * outerRadius;

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var deltaX = x - center.X;
            var deltaY = y - center.Y;
            var distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared < innerRadiusSquared || distanceSquared > outerRadiusSquared)
                continue;

            AddPixel(y * World.Width + x, red, green, blue);
        }
    }

    private void AddPixel(int pixelIndex, int red, int green, int blue)
    {
        var byteIndex = pixelIndex * 4;
        m_pixels[byteIndex] = (byte)Math.Min(255, m_pixels[byteIndex] + blue);
        m_pixels[byteIndex + 1] = (byte)Math.Min(255, m_pixels[byteIndex + 1] + green);
        m_pixels[byteIndex + 2] = (byte)Math.Min(255, m_pixels[byteIndex + 2] + red);
    }

    private static int ClampByte(float value) =>
        Math.Clamp((int)value, 0, 255);

    private static int ScalePheromone(float value) =>
        ClampByte(MathF.Sqrt(value) * 72);
}
