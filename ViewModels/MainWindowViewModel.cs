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
using G33kColony.Services;

namespace G33kColony.ViewModels;

/// <summary>
/// Provides the main simulation state for G33kColony.
/// </summary>
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IAppSettings m_settings;
    private readonly DispatcherTimer m_timer;
    private World m_world;
    private Colony m_colony;
    private int m_frameNumber;
    private int m_antCount = AppSettings.DefaultAntCount;
    private int m_antMaximumLife = AppSettings.DefaultAntMaximumLife;
    private int m_foodSourceCount = AppSettings.DefaultFoodSourceCount;
    private int m_stepsPerTick = AppSettings.DefaultStepsPerTick;
    private double m_pheromoneDecayRate = AppSettings.DefaultPheromoneDecayRate;
    private double m_turnChance = AppSettings.DefaultTurnChance;
    private double m_randomTurnDegrees = AppSettings.DefaultRandomTurnDegrees;
    private double m_foodTrailIgnoreChance = AppSettings.DefaultFoodTrailIgnoreChance;
    private double m_pheromoneDepositAmount = AppSettings.DefaultPheromoneDepositAmount;
    private bool m_isHomePheromoneVisible = AppSettings.DefaultIsHomePheromoneVisible;
    private bool m_isFoodPheromoneVisible = AppSettings.DefaultIsFoodPheromoneVisible;
    private bool m_isSensorOverlayVisible = AppSettings.DefaultIsSensorOverlayVisible;
    private bool m_isDisposed;
    private string m_seedText = AppSettings.CreateDefaultSeedText();

    public MainWindowViewModel()
        : this(true, new AppSettings())
    {
    }

    internal MainWindowViewModel(bool startTimer, IAppSettings settings = null)
    {
        m_settings = settings;
        GoCommand = new RelayCommand(_ => RestartGame());
        NewSeedCommand = new RelayCommand(_ => NewSeed());
        ResetSettingsCommand = new RelayCommand(_ => ResetSettings());

        if (m_settings != null)
            ApplySettingsToFields(m_settings);

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

    public ICommand ResetSettingsCommand { get; }

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

    public double FoodTrailIgnoreChance
    {
        get => m_foodTrailIgnoreChance;
        set
        {
            if (SetField(ref m_foodTrailIgnoreChance, Math.Clamp(value, 0, 0.35)))
            {
                OnPropertyChanged(nameof(FoodTrailIgnoreChancePercent));
                ApplyColonySettings();
            }
        }
    }

    public double FoodTrailIgnoreChancePercent => FoodTrailIgnoreChance * 100;

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
            if (SetField(ref m_antCount, Math.Clamp(value, AppSettings.MinimumAntCount, AppSettings.MaximumAntCount)))
                RestartGame();
        }
    }

    public int AntCountMinimum => AppSettings.MinimumAntCount;

    public int AntCountMaximum => AppSettings.MaximumAntCount;

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

    public bool IsSensorOverlayVisible
    {
        get => m_isSensorOverlayVisible;
        set => SetField(ref m_isSensorOverlayVisible, value);
    }

    public int FrameNumber
    {
        get => m_frameNumber;
        private set => SetField(ref m_frameNumber, value);
    }

    public int FoodFoundCount => Colony?.FoodFoundCount ?? 0;

    public int FoodReturnedHomeCount => Colony?.FoodReturnedHomeCount ?? 0;

    public int FoodRemaining => World?.FoodRemaining ?? 0;

    public long WorldTickCount { get; private set; }

    public void Tick()
    {
        for (var i = 0; i < StepsPerTick; i++)
        {
            Colony.Tick();
            
            if (World.FoodRemaining > 0)
                WorldTickCount++;
        }

        World.Tick((float)(1 - PheromoneDecayRate));
        OnPropertyChanged(nameof(FoodFoundCount));
        OnPropertyChanged(nameof(FoodReturnedHomeCount));
        OnPropertyChanged(nameof(FoodRemaining));
        OnPropertyChanged(nameof(WorldTickCount));
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
        WorldTickCount = 0;
        ApplyColonySettings();
        OnPropertyChanged(nameof(FoodFoundCount));
        OnPropertyChanged(nameof(FoodReturnedHomeCount));
        OnPropertyChanged(nameof(FoodRemaining));
        OnPropertyChanged(nameof(WorldTickCount));
    }

    private void NewSeed()
    {
        SeedText = Random.Shared.Next(1, int.MaxValue).ToString();
        RestartGame();
    }

    public void Dispose()
    {
        if (m_isDisposed)
            return;

        m_isDisposed = true;
        m_timer.Tick -= OnTimerTick;
        m_timer.Stop();
        SaveSettings();
        m_settings?.Dispose();
    }

    private void ResetSettings()
    {
        ApplyDefaultSettingsToFields();
        RestartGame();
        RaiseAllPropertiesChanged();
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
        Colony.FoodTrailIgnoreChance = FoodTrailIgnoreChance;
        Colony.PheromoneDepositAmount = (float)PheromoneDepositAmount;
        Colony.AntMaximumLife = AntMaximumLife;
    }

    private void ApplySettingsToFields(IAppSettings settings)
    {
        m_seedText = string.IsNullOrWhiteSpace(settings.SeedText)
            ? AppSettings.CreateDefaultSeedText()
            : settings.SeedText;
        m_pheromoneDecayRate = Math.Clamp(settings.PheromoneDecayRate, 0.001, 0.08);
        m_pheromoneDepositAmount = Math.Clamp(settings.PheromoneDepositAmount, 0.1, 8);
        m_turnChance = Math.Clamp(settings.TurnChance, 0, 1);
        m_randomTurnDegrees = Math.Clamp(settings.RandomTurnDegrees, 5, 75);
        m_foodTrailIgnoreChance = Math.Clamp(settings.FoodTrailIgnoreChance, 0, 0.35);
        m_stepsPerTick = Math.Clamp(settings.StepsPerTick, 1, 6);
        m_antCount = Math.Clamp(settings.AntCount, AppSettings.MinimumAntCount, AppSettings.MaximumAntCount);
        m_antMaximumLife = Math.Clamp(settings.AntMaximumLife, 25, 5000);
        m_foodSourceCount = Math.Clamp(settings.FoodSourceCount, 1, 4);
        m_isHomePheromoneVisible = settings.IsHomePheromoneVisible;
        m_isFoodPheromoneVisible = settings.IsFoodPheromoneVisible;
        m_isSensorOverlayVisible = settings.IsSensorOverlayVisible;
    }

    private void ApplyDefaultSettingsToFields()
    {
        m_seedText = AppSettings.CreateDefaultSeedText();
        m_pheromoneDecayRate = AppSettings.DefaultPheromoneDecayRate;
        m_pheromoneDepositAmount = AppSettings.DefaultPheromoneDepositAmount;
        m_turnChance = AppSettings.DefaultTurnChance;
        m_randomTurnDegrees = AppSettings.DefaultRandomTurnDegrees;
        m_foodTrailIgnoreChance = AppSettings.DefaultFoodTrailIgnoreChance;
        m_stepsPerTick = AppSettings.DefaultStepsPerTick;
        m_antCount = AppSettings.DefaultAntCount;
        m_antMaximumLife = AppSettings.DefaultAntMaximumLife;
        m_foodSourceCount = AppSettings.DefaultFoodSourceCount;
        m_isHomePheromoneVisible = AppSettings.DefaultIsHomePheromoneVisible;
        m_isFoodPheromoneVisible = AppSettings.DefaultIsFoodPheromoneVisible;
        m_isSensorOverlayVisible = AppSettings.DefaultIsSensorOverlayVisible;
    }

    private void SaveSettings()
    {
        if (m_settings == null)
            return;

        m_settings.SeedText = SeedText;
        m_settings.PheromoneDecayRate = PheromoneDecayRate;
        m_settings.PheromoneDepositAmount = PheromoneDepositAmount;
        m_settings.TurnChance = TurnChance;
        m_settings.RandomTurnDegrees = RandomTurnDegrees;
        m_settings.FoodTrailIgnoreChance = FoodTrailIgnoreChance;
        m_settings.StepsPerTick = StepsPerTick;
        m_settings.AntCount = AntCount;
        m_settings.AntMaximumLife = AntMaximumLife;
        m_settings.FoodSourceCount = FoodSourceCount;
        m_settings.IsHomePheromoneVisible = IsHomePheromoneVisible;
        m_settings.IsFoodPheromoneVisible = IsFoodPheromoneVisible;
        m_settings.IsSensorOverlayVisible = IsSensorOverlayVisible;
    }
}
