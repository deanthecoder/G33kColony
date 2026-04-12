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
using DTC.Core.Settings;

namespace G33kColony.Services;

/// <summary>
/// Stores G33kColony user interface settings using the shared DTC.Core settings file infrastructure.
/// </summary>
internal sealed class AppSettings : UserSettingsBase, IAppSettings
{
    public const int MinimumAntCount = 1;
    public const int MaximumAntCount = 2000;
    public const int DefaultAntCount = 500;
    public const int DefaultAntMaximumLife = 500;
    public const int DefaultFoodSourceCount = 2;
    public const int DefaultStepsPerTick = 1;
    public const double DefaultPheromoneDecayRate = 0.01;
    public const double DefaultPheromoneDepositAmount = 4.0;
    public const double DefaultTurnChance = 0.5;
    public const double DefaultRandomTurnDegrees = 20.0;
    public const bool DefaultIsHomePheromoneVisible = false;
    public const bool DefaultIsFoodPheromoneVisible = false;
    public const bool DefaultIsSensorOverlayVisible = false;

    protected override string SettingsFileName => "ui-settings.json";

    public string SeedText
    {
        get => Get<string>();
        set => Set(value);
    }

    public double PheromoneDecayRate
    {
        get => Get<double>();
        set => Set(value);
    }

    public double PheromoneDepositAmount
    {
        get => Get<double>();
        set => Set(value);
    }

    public double TurnChance
    {
        get => Get<double>();
        set => Set(value);
    }

    public double RandomTurnDegrees
    {
        get => Get<double>();
        set => Set(value);
    }

    public int StepsPerTick
    {
        get => Get<int>();
        set => Set(value);
    }

    public int AntCount
    {
        get => Get<int>();
        set => Set(value);
    }

    public int AntMaximumLife
    {
        get => Get<int>();
        set => Set(value);
    }

    public int FoodSourceCount
    {
        get => Get<int>();
        set => Set(value);
    }

    public bool IsHomePheromoneVisible
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool IsFoodPheromoneVisible
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool IsSensorOverlayVisible
    {
        get => Get<bool>();
        set => Set(value);
    }

    public static string CreateDefaultSeedText() =>
        Random.Shared.Next(1, int.MaxValue).ToString();

    protected override void ApplyDefaults()
    {
        SeedText = CreateDefaultSeedText();
        PheromoneDecayRate = DefaultPheromoneDecayRate;
        PheromoneDepositAmount = DefaultPheromoneDepositAmount;
        TurnChance = DefaultTurnChance;
        RandomTurnDegrees = DefaultRandomTurnDegrees;
        StepsPerTick = DefaultStepsPerTick;
        AntCount = DefaultAntCount;
        AntMaximumLife = DefaultAntMaximumLife;
        FoodSourceCount = DefaultFoodSourceCount;
        IsHomePheromoneVisible = DefaultIsHomePheromoneVisible;
        IsFoodPheromoneVisible = DefaultIsFoodPheromoneVisible;
        IsSensorOverlayVisible = DefaultIsSensorOverlayVisible;
    }
}
