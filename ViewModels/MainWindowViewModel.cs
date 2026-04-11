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
using System.Windows.Input;
using Avalonia.Threading;
using DTC.Core.Commands;
using DTC.Core.ViewModels;
using G33kColony.Models;

namespace G33kColony.ViewModels;

/// <summary>
/// Provides the main simulation state for G33kColony.
/// </summary>
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer m_timer;
    private World m_world;
    private Colony m_colony;
    private int m_frameNumber;
    private int m_antCount = 100;
    private int m_antMaximumLife = 1000;
    private int m_foodSourceCount = 1;
    private int m_stepsPerTick = 1;
    private double m_pheromoneDecayRate = 0.012;
    private double m_turnChance = 0.55;
    private double m_randomTurnDegrees = 35;
    private double m_pheromoneDepositAmount = 2.4;
    private bool m_isHomePheromoneVisible = true;
    private bool m_isFoodPheromoneVisible = true;
    private string m_seedText = Random.Shared.Next(1, int.MaxValue).ToString();

    public MainWindowViewModel()
        : this(true)
    {
    }

    internal MainWindowViewModel(bool startTimer)
    {
        GoCommand = new RelayCommand(_ => RestartGame());
        NewSeedCommand = new RelayCommand(_ => NewSeed());

        RestartGame();

        m_timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        m_timer.Tick += OnTimerTick;

        if (startTimer)
            m_timer.Start();
    }

    public static string AppName => "G33kColony";

    public static string WindowTitle => "G33kColony";

    public static string Tagline => "Ant colony simulation workspace.";

    public ICommand GoCommand { get; }

    public ICommand NewSeedCommand { get; }

    public World World
    {
        get => m_world;
        private set => SetField(ref m_world, value);
    }

    public Colony Colony
    {
        get => m_colony;
        private set => SetField(ref m_colony, value);
    }

    public string SeedText
    {
        get => m_seedText;
        set => SetField(ref m_seedText, value);
    }

    public double PheromoneDecayRate
    {
        get => m_pheromoneDecayRate;
        set
        {
            if (SetField(ref m_pheromoneDecayRate, Math.Clamp(value, 0.001, 0.08)))
                OnPropertyChanged(nameof(PheromoneDecayRatePercent));
        }
    }

    public double PheromoneDecayRatePercent => PheromoneDecayRate * 100;

    public double PheromoneDepositAmount
    {
        get => m_pheromoneDepositAmount;
        set
        {
            if (SetField(ref m_pheromoneDepositAmount, Math.Clamp(value, 0.1, 8)))
                ApplyColonySettings();
        }
    }

    public double TurnChance
    {
        get => m_turnChance;
        set
        {
            if (SetField(ref m_turnChance, Math.Clamp(value, 0, 1)))
            {
                OnPropertyChanged(nameof(TurnChancePercent));
                ApplyColonySettings();
            }
        }
    }

    public double TurnChancePercent => TurnChance * 100;

    public double RandomTurnDegrees
    {
        get => m_randomTurnDegrees;
        set
        {
            if (SetField(ref m_randomTurnDegrees, Math.Clamp(value, 5, 75)))
                ApplyColonySettings();
        }
    }

    public int StepsPerTick
    {
        get => m_stepsPerTick;
        set => SetField(ref m_stepsPerTick, Math.Clamp(value, 1, 6));
    }

    public int AntCount
    {
        get => m_antCount;
        set
        {
            if (SetField(ref m_antCount, Math.Clamp(value, 25, 1000)))
                RestartGame();
        }
    }

    public int AntMaximumLife
    {
        get => m_antMaximumLife;
        set
        {
            if (SetField(ref m_antMaximumLife, Math.Clamp(value, 25, 5000)))
                ApplyColonySettings();
        }
    }

    public int FoodSourceCount
    {
        get => m_foodSourceCount;
        set
        {
            if (SetField(ref m_foodSourceCount, Math.Clamp(value, 1, 4)))
                RestartGame();
        }
    }

    public bool IsHomePheromoneVisible
    {
        get => m_isHomePheromoneVisible;
        set => SetField(ref m_isHomePheromoneVisible, value);
    }

    public bool IsFoodPheromoneVisible
    {
        get => m_isFoodPheromoneVisible;
        set => SetField(ref m_isFoodPheromoneVisible, value);
    }

    public int FrameNumber
    {
        get => m_frameNumber;
        private set => SetField(ref m_frameNumber, value);
    }

    public int FoodFoundCount => Colony?.FoodFoundCount ?? 0;

    public int FoodReturnedHomeCount => Colony?.FoodReturnedHomeCount ?? 0;

    public void Tick()
    {
        for (var i = 0; i < StepsPerTick; i++)
            Colony.Tick();

        World.Tick((float)(1 - PheromoneDecayRate));
        OnPropertyChanged(nameof(FoodFoundCount));
        OnPropertyChanged(nameof(FoodReturnedHomeCount));
        FrameNumber++;
    }

    public void RestartGame()
    {
        World = new World(
            World.DefaultWidth,
            World.DefaultHeight,
            FoodSourceCount,
            GetCurrentSeed());
        Colony = new Colony(World, AntCount, GetCurrentSeed());
        ApplyColonySettings();
        OnPropertyChanged(nameof(FoodFoundCount));
        OnPropertyChanged(nameof(FoodReturnedHomeCount));
    }

    public void NewSeed()
    {
        SeedText = Random.Shared.Next(1, int.MaxValue).ToString();
        RestartGame();
    }

    public void Dispose()
    {
        m_timer.Tick -= OnTimerTick;
        m_timer.Stop();
    }

    private void OnTimerTick(object sender, EventArgs e) =>
        Tick();

    private int GetCurrentSeed()
    {
        if (int.TryParse(SeedText, out var seed))
            return seed;

        return 33;
    }

    private void ApplyColonySettings()
    {
        if (Colony == null)
            return;

        Colony.TurnChance = TurnChance;
        Colony.MaximumRandomTurnRadians = RandomTurnDegrees * Math.PI / 180;
        Colony.PheromoneDepositAmount = (float)PheromoneDepositAmount;
        Colony.AntMaximumLife = AntMaximumLife;
    }
}
