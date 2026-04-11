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
using DTC.Core;
using G33kColony.Models;

namespace G33kColony.Views;

/// <summary>
/// Renders the fixed-size simulation world to a bitmap that scales uniformly to fit the view.
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
        var world = World;
        var homeCells = world.HomePheromones.Cells;
        var foodCells = world.FoodPheromones.Cells;

        for (var i = 0; i < homeCells.Length; i++)
        {
            var blue = 18;
            var green = 18;
            var red = 18;

            if (ShowHomePheromones)
            {
                var intensity = ScalePheromone(homeCells[i]);
                green = Math.Min(255, green + intensity);
                blue = Math.Min(255, blue + intensity);
            }

            if (ShowFoodPheromones)
            {
                var intensity = ScalePheromone(foodCells[i]);
                red = Math.Min(255, red + intensity);
                green = Math.Min(255, green + intensity / 4);
            }

            SetPixel(i, red, green, blue);
        }

        DrawFoodSources();
        DrawNest();
        DrawAnts();
    }

    private void DrawFoodSources()
    {
        foreach (var source in World.FoodSources)
        {
            for (var y = source.Position.Y - source.Radius; y <= source.Position.Y + source.Radius; y++)
            {
                for (var x = source.Position.X - source.Radius; x <= source.Position.X + source.Radius; x++)
                {
                    var position = new IntPoint(x, y);
                    if (World.FoodPheromones.Contains(x, y) && source.Contains(position))
                        SetPixel(y * World.Width + x, 120, 230, 70);
                }
            }
        }
    }

    private void DrawNest()
    {
        var nest = World.NestPosition;
        for (var y = nest.Y - 3; y <= nest.Y + 3; y++)
        {
            for (var x = nest.X - 3; x <= nest.X + 3; x++)
            {
                if (World.HomePheromones.Contains(x, y))
                    SetPixel(y * World.Width + x, 230, 70, 55);
            }
        }
    }

    private void DrawAnts()
    {
        if (Colony == null)
            return;

        foreach (var ant in Colony.Ants)
        {
            if (!ant.IsAlive)
                continue;

            var index = ant.Position.Y * World.Width + ant.Position.X;
            if (ant.State == AntState.Returning)
                SetPixel(index, 235, 55, 45);
            else
                SetPixel(index, 245, 245, 230);
        }
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

    private static int ClampByte(float value) =>
        Math.Clamp((int)value, 0, 255);

    private static int ScalePheromone(float value) =>
        ClampByte(MathF.Sqrt(value) * 72);
}
