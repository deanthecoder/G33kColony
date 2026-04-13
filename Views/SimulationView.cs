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
using Avalonia.Input;
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
    private const int AntBodyLengthPixels = 3;
    private const double ObstacleBrushRadius = 6;

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
    private WriteableBitmap m_pheromoneBitmap;
    private byte[] m_pixels;
    private byte[] m_pheromonePixels;
    private bool m_isDrawingObstacle;
    private bool m_isErasingObstacle;

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

        if ((ShowHomePheromones || ShowFoodPheromones) && m_pheromoneBitmap != null)
        {
            context.DrawImage(
                m_pheromoneBitmap,
                new Rect(0, 0, m_pheromoneBitmap.PixelSize.Width, m_pheromoneBitmap.PixelSize.Height),
                destination);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResetBitmap();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (World == null)
            return;

        var point = e.GetCurrentPoint(this);
        var shouldErase = ShouldErase(point, e.KeyModifiers);
        var shouldDraw = point.Properties.IsLeftButtonPressed && !shouldErase;
        if (!shouldDraw && !shouldErase)
            return;

        if (!TryGetWorldPosition(e.GetPosition(this), out var worldPosition))
            return;

        m_isDrawingObstacle = shouldDraw;
        m_isErasingObstacle = shouldErase;
        ApplyObstacleBrush(worldPosition);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!m_isDrawingObstacle && !m_isErasingObstacle)
            return;

        var point = e.GetCurrentPoint(this);
        var shouldErase = ShouldErase(point, e.KeyModifiers);
        var shouldDraw = point.Properties.IsLeftButtonPressed && !shouldErase;
        if (!shouldDraw && !shouldErase)
            return;

        m_isDrawingObstacle = shouldDraw;
        m_isErasingObstacle = shouldErase;
        if (World == null || !TryGetWorldPosition(e.GetPosition(this), out var worldPosition))
            return;

        ApplyObstacleBrush(worldPosition);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        m_isDrawingObstacle = false;
        m_isErasingObstacle = false;
        e.Pointer.Capture(null);
    }

    private void RenderWorld()
    {
        if (World == null)
            return;

        EnsureBitmap();
        if (m_bitmap == null)
            return;

        DrawWorldPixels();
        DrawPheromoneOverlayPixels();

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
            EnsurePheromoneBitmap();
            return;
        }

        ResetBitmap();
        m_pixels = new byte[World.Width * World.Height * 4];
        m_bitmap = new WriteableBitmap(
            new PixelSize(World.Width, World.Height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque);
        EnsurePheromoneBitmap();
    }

    private void ResetBitmap()
    {
        m_bitmap?.Dispose();
        m_pheromoneBitmap?.Dispose();
        m_bitmap = null;
        m_pheromoneBitmap = null;
        m_pixels = null;
        m_pheromonePixels = null;
    }

    private void DrawWorldPixels()
    {
        for (var i = 0; i < World.Width * World.Height; i++)
            SetPixel(i, 18, 18, 18);

        DrawObstacles();
        DrawFoodSources();
        DrawNest();
        DrawSensorOverlay();
        DrawAnts();
    }

    private void EnsurePheromoneBitmap()
    {
        var gridWidth = World.HomePheromones.GridWidth;
        var gridHeight = World.HomePheromones.GridHeight;
        if (m_pheromoneBitmap != null &&
            m_pheromoneBitmap.PixelSize.Width == gridWidth &&
            m_pheromoneBitmap.PixelSize.Height == gridHeight)
        {
            return;
        }

        m_pheromoneBitmap?.Dispose();
        m_pheromonePixels = new byte[gridWidth * gridHeight * 4];
        m_pheromoneBitmap = new WriteableBitmap(
            new PixelSize(gridWidth, gridHeight),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Unpremul);
    }

    private void DrawPheromoneOverlayPixels()
    {
        if (m_pheromoneBitmap == null || m_pheromonePixels == null)
            return;

        Array.Clear(m_pheromonePixels, 0, m_pheromonePixels.Length);
        if (ShowHomePheromones)
            DrawPheromoneGrid(World.HomePheromones, 1.0, 0.32, 0.32);

        if (ShowFoodPheromones)
            DrawPheromoneGrid(World.FoodPheromones, 1.0, 0.82, 0.28);

        using var framebuffer = m_pheromoneBitmap.Lock();
        var sourceRowBytes = m_pheromoneBitmap.PixelSize.Width * 4;
        if (framebuffer.RowBytes == sourceRowBytes)
        {
            Marshal.Copy(m_pheromonePixels, 0, framebuffer.Address, m_pheromonePixels.Length);
            return;
        }

        for (var y = 0; y < m_pheromoneBitmap.PixelSize.Height; y++)
        {
            var sourceOffset = y * sourceRowBytes;
            var target = IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes);
            Marshal.Copy(m_pheromonePixels, sourceOffset, target, sourceRowBytes);
        }
    }

    private void DrawObstacles()
    {
        if (!World.HasObstacles)
            return;

        for (var y = 0; y < World.Height; y++)
        {
            for (var x = 0; x < World.Width; x++)
            {
                if (!World.IsObstacleCell(x, y))
                    continue;

                SetPixel(y * World.Width + x, 95, 95, 105);
            }
        }
    }

    private void DrawPheromoneGrid(PheromoneField pheromones, double redScale, double greenScale, double blueScale)
    {
        for (var y = 0; y < pheromones.GridHeight; y++)
        for (var x = 0; x < pheromones.GridWidth; x++)
        {
            var strength = pheromones.GetCellStrength(x, y);
            if (strength <= 0)
                continue;

            var intensity = ScalePheromone(strength);
            AddOverlayPixel(
                y * pheromones.GridWidth + x,
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
            AddCircle(source.Position, source.Radius, (int)(175 + 70 * ratio), (int)(145 + 90 * ratio), 55);
        }
    }

    private void DrawNest()
    {
        FillCircle(World.NestPosition, Colony.NestRadius, 255, 145, 150);
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
                DrawAntBody(ant, index, 250, 220, 90);
            else if (ant.IsInLaunchPhase)
                DrawAntBody(ant, index, 255, 135, 140);
            else
                DrawAntBody(ant, index, 255, 175, 178);
        }
    }

    private void DrawAntBody(Ant ant, int headIndex, int red, int green, int blue)
    {
        for (var i = AntBodyLengthPixels; i > 0; i--)
        {
            var tailX = (int)Math.Round(ant.Position.X - ant.DirectionX * i);
            var tailY = (int)Math.Round(ant.Position.Y - ant.DirectionY * i);
            if (tailX < 0 || tailY < 0 || tailX >= World.Width || tailY >= World.Height)
                continue;

            var brightness = 0.45 + 0.15 * (AntBodyLengthPixels - i);
            SetPixel(
                tailY * World.Width + tailX,
                (int)(red * brightness),
                (int)(green * brightness),
                (int)(blue * brightness));
        }

        SetPixel(headIndex, red, green, blue);
    }

    private void DrawSensorOverlay()
    {
        if (!ShowSensorOverlay || Colony == null)
            return;

        foreach (var ant in Colony.Ants)
        {
            if (!ant.IsAlive)
                continue;

            DrawAntBodyRadius(ant);
            DrawSensorArea(ant, -Colony.SensorAngleRadians, 40, 120, 255);
            DrawSensorArea(ant, 0, 245, 245, 245);
            DrawSensorArea(ant, Colony.SensorAngleRadians, 255, 210, 50);
        }
    }

    private void DrawAntBodyRadius(Ant ant)
    {
        AddCircle(ant.Position, Colony.AntRadius, 60, 60, 60);
        AddCircleOutline(ant.Position, Colony.AntRadius, 255, 120, 120);
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

    private void ApplyObstacleBrush(WorldPoint worldPosition)
    {
        var isObstacle = !m_isErasingObstacle;
        World.SetObstacleCircle(worldPosition, ObstacleBrushRadius, isObstacle);
        RenderWorld();
        InvalidateVisual();
    }

    private bool TryGetWorldPosition(Point viewPosition, out WorldPoint worldPosition)
    {
        worldPosition = WorldPoint.Zero;
        if (World == null)
            return false;

        var destination = GetUniformDestinationRect(World.Width, World.Height);
        if (destination.Width <= 0 || destination.Height <= 0 || !destination.Contains(viewPosition))
            return false;

        var worldX = (viewPosition.X - destination.X) * World.Width / destination.Width;
        var worldY = (viewPosition.Y - destination.Y) * World.Height / destination.Height;
        worldPosition = new WorldPoint(
            Math.Clamp(worldX, 0, World.Width - 1),
            Math.Clamp(worldY, 0, World.Height - 1));
        return true;
    }

    private static bool ShouldErase(PointerPoint point, KeyModifiers modifiers)
    {
        if (point.Properties.IsRightButtonPressed)
            return true;

        var eraseModifierHeld = modifiers.HasFlag(KeyModifiers.Alt) || modifiers.HasFlag(KeyModifiers.Control);
        return eraseModifierHeld && point.Properties.IsLeftButtonPressed;
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

    private void FillCircle(WorldPoint center, double radius, int red, int green, int blue)
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
            if (deltaX * deltaX + deltaY * deltaY > radiusSquared)
                continue;

            SetPixel(y * World.Width + x, red, green, blue);
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

    private void AddOverlayPixel(int pixelIndex, int red, int green, int blue)
    {
        var byteIndex = pixelIndex * 4;
        m_pheromonePixels[byteIndex] = (byte)Math.Min(255, m_pheromonePixels[byteIndex] + blue);
        m_pheromonePixels[byteIndex + 1] = (byte)Math.Min(255, m_pheromonePixels[byteIndex + 1] + green);
        m_pheromonePixels[byteIndex + 2] = (byte)Math.Min(255, m_pheromonePixels[byteIndex + 2] + red);
        m_pheromonePixels[byteIndex + 3] = (byte)Math.Min(220, m_pheromonePixels[byteIndex + 3] + 90);
    }

    private static int ClampByte(float value) =>
        Math.Clamp((int)value, 0, 255);

    private static int ScalePheromone(float value) =>
        ClampByte(MathF.Sqrt(value) * 72);
}
