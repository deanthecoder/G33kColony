// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using G33kColony.Models;
using G33kColony.Services;
using G33kColony.ViewModels;

namespace G33kColony.Tests;

public class MainWindowViewModelTests
{
    [Test]
    public void MainWindowViewModelExposesProductText()
    {
        using var viewModel = new MainWindowViewModel(false);

        Assert.That(MainWindowViewModel.AppName, Is.EqualTo("G33kColony"));
        Assert.That(MainWindowViewModel.WindowTitle, Is.EqualTo("G33kColony"));
        Assert.That(MainWindowViewModel.Tagline, Does.Contain("Ant colony"));
    }

    [Test]
    public void MainWindowViewModelExposesSimulationState()
    {
        using var viewModel = new MainWindowViewModel(false);

        Assert.That(viewModel.World.Width, Is.EqualTo(640));
        Assert.That(viewModel.World.Height, Is.EqualTo(480));
        Assert.That(viewModel.Colony.Ants, Has.Count.EqualTo(viewModel.AntCount));
        Assert.That(viewModel.World.FoodSources, Has.Count.EqualTo(viewModel.FoodSourceCount * World.FoodBlobsPerSource));
        Assert.That(viewModel.FoodSourceCount, Is.EqualTo(AppSettings.DefaultFoodSourceCount));
        Assert.That(viewModel.IsHomePheromoneVisible, Is.EqualTo(AppSettings.DefaultIsHomePheromoneVisible));
        Assert.That(viewModel.IsFoodPheromoneVisible, Is.EqualTo(AppSettings.DefaultIsFoodPheromoneVisible));
        Assert.That(viewModel.IsSensorOverlayVisible, Is.False);
    }

    [Test]
    public void SensorOverlayVisibilityCanBeToggled()
    {
        using var viewModel = new MainWindowViewModel(false);

        viewModel.IsSensorOverlayVisible = true;

        Assert.That(viewModel.IsSensorOverlayVisible, Is.True);
    }

    [Test]
    public void MainWindowViewModelLoadsSavedSettings()
    {
        using var settings = new TestAppSettings
        {
            SeedText = "42",
            AntCount = 22,
            AntMaximumLife = 125,
            FoodSourceCount = 3,
            StepsPerTick = 4,
            PheromoneDecayRate = 0.02,
            PheromoneDepositAmount = 3.5,
            TurnChance = 0.25,
            RandomTurnDegrees = 18,
            FoodTrailIgnoreChance = 0.12,
            IsHomePheromoneVisible = false,
            IsFoodPheromoneVisible = false,
            IsSensorOverlayVisible = true
        };

        using var viewModel = new MainWindowViewModel(false, settings);

        Assert.That(viewModel.SeedText, Is.EqualTo("42"));
        Assert.That(viewModel.AntCount, Is.EqualTo(22));
        Assert.That(viewModel.AntMaximumLife, Is.EqualTo(125));
        Assert.That(viewModel.FoodSourceCount, Is.EqualTo(3));
        Assert.That(viewModel.StepsPerTick, Is.EqualTo(4));
        Assert.That(viewModel.PheromoneDecayRate, Is.EqualTo(0.02));
        Assert.That(viewModel.PheromoneDepositAmount, Is.EqualTo(3.5));
        Assert.That(viewModel.TurnChance, Is.EqualTo(0.25));
        Assert.That(viewModel.RandomTurnDegrees, Is.EqualTo(18));
        Assert.That(viewModel.FoodTrailIgnoreChance, Is.EqualTo(0.12));
        Assert.That(viewModel.IsHomePheromoneVisible, Is.False);
        Assert.That(viewModel.IsFoodPheromoneVisible, Is.False);
        Assert.That(viewModel.IsSensorOverlayVisible, Is.True);
        Assert.That(viewModel.Colony.Ants, Has.Count.EqualTo(22));
        Assert.That(viewModel.World.FoodSources, Has.Count.EqualTo(3 * World.FoodBlobsPerSource));
    }

    [Test]
    public void MainWindowViewModelSavesSettingsWhenDisposed()
    {
        var settings = new TestAppSettings();
        var viewModel = new MainWindowViewModel(false, settings)
        {
            SeedText = "77",
            AntCount = 33,
            AntMaximumLife = 250,
            FoodSourceCount = 2,
            StepsPerTick = 5,
            PheromoneDecayRate = 0.03,
            PheromoneDepositAmount = 4.5,
            TurnChance = 0.75,
            RandomTurnDegrees = 44,
            FoodTrailIgnoreChance = 0.2,
            IsHomePheromoneVisible = false,
            IsFoodPheromoneVisible = false,
            IsSensorOverlayVisible = true
        };

        viewModel.Dispose();

        Assert.That(settings.IsDisposed, Is.True);
        Assert.That(settings.SeedText, Is.EqualTo("77"));
        Assert.That(settings.AntCount, Is.EqualTo(33));
        Assert.That(settings.AntMaximumLife, Is.EqualTo(250));
        Assert.That(settings.FoodSourceCount, Is.EqualTo(2));
        Assert.That(settings.StepsPerTick, Is.EqualTo(5));
        Assert.That(settings.PheromoneDecayRate, Is.EqualTo(0.03));
        Assert.That(settings.PheromoneDepositAmount, Is.EqualTo(4.5));
        Assert.That(settings.TurnChance, Is.EqualTo(0.75));
        Assert.That(settings.RandomTurnDegrees, Is.EqualTo(44));
        Assert.That(settings.FoodTrailIgnoreChance, Is.EqualTo(0.2));
        Assert.That(settings.IsHomePheromoneVisible, Is.False);
        Assert.That(settings.IsFoodPheromoneVisible, Is.False);
        Assert.That(settings.IsSensorOverlayVisible, Is.True);
    }

    [Test]
    public void ResetSettingsCommandRestoresDefaults()
    {
        using var viewModel = new MainWindowViewModel(false)
        {
            AntCount = 75,
            AntMaximumLife = 250,
            FoodSourceCount = 4,
            StepsPerTick = 6,
            PheromoneDecayRate = 0.04,
            PheromoneDepositAmount = 6,
            TurnChance = 0.9,
            RandomTurnDegrees = 65,
            FoodTrailIgnoreChance = 0.3,
            IsHomePheromoneVisible = false,
            IsFoodPheromoneVisible = false,
            IsSensorOverlayVisible = true
        };

        viewModel.ResetSettingsCommand.Execute(null);

        Assert.That(viewModel.AntCount, Is.EqualTo(AppSettings.DefaultAntCount));
        Assert.That(viewModel.AntMaximumLife, Is.EqualTo(AppSettings.DefaultAntMaximumLife));
        Assert.That(viewModel.FoodSourceCount, Is.EqualTo(AppSettings.DefaultFoodSourceCount));
        Assert.That(viewModel.StepsPerTick, Is.EqualTo(AppSettings.DefaultStepsPerTick));
        Assert.That(viewModel.PheromoneDecayRate, Is.EqualTo(AppSettings.DefaultPheromoneDecayRate));
        Assert.That(viewModel.PheromoneDepositAmount, Is.EqualTo(AppSettings.DefaultPheromoneDepositAmount));
        Assert.That(viewModel.TurnChance, Is.EqualTo(AppSettings.DefaultTurnChance));
        Assert.That(viewModel.RandomTurnDegrees, Is.EqualTo(AppSettings.DefaultRandomTurnDegrees));
        Assert.That(viewModel.FoodTrailIgnoreChance, Is.EqualTo(AppSettings.DefaultFoodTrailIgnoreChance));
        Assert.That(viewModel.IsHomePheromoneVisible, Is.EqualTo(AppSettings.DefaultIsHomePheromoneVisible));
        Assert.That(viewModel.IsFoodPheromoneVisible, Is.EqualTo(AppSettings.DefaultIsFoodPheromoneVisible));
        Assert.That(viewModel.IsSensorOverlayVisible, Is.EqualTo(AppSettings.DefaultIsSensorOverlayVisible));
        Assert.That(viewModel.Colony.Ants, Has.Count.EqualTo(AppSettings.DefaultAntCount));
        Assert.That(viewModel.World.FoodSources, Has.Count.EqualTo(AppSettings.DefaultFoodSourceCount * World.FoodBlobsPerSource));
    }

    [Test]
    public void TickAdvancesFrameNumber()
    {
        using var viewModel = new MainWindowViewModel(false);

        viewModel.Tick();

        Assert.That(viewModel.FrameNumber, Is.EqualTo(1));
    }

    [Test]
    public void RestartGameUsesCurrentSeed()
    {
        using var viewModel = new MainWindowViewModel(false);
        viewModel.SeedText = "12";

        viewModel.RestartGame();
        var firstPosition = viewModel.Colony.Ants[0].Position;
        viewModel.Tick();
        var firstTickPosition = viewModel.Colony.Ants[0].Position;

        viewModel.RestartGame();
        viewModel.Tick();

        Assert.That(viewModel.Colony.Ants[0].Position, Is.EqualTo(firstTickPosition));
        Assert.That(firstTickPosition, Is.Not.EqualTo(firstPosition));
    }

    [Test]
    public void SliderSettingsHaveImmediateEffect()
    {
        using var viewModel = new MainWindowViewModel(false);

        viewModel.PheromoneDepositAmount = 4.5;
        viewModel.TurnChance = 0.75;
        viewModel.RandomTurnDegrees = 35;
        viewModel.FoodTrailIgnoreChance = 0.18;
        viewModel.PheromoneDecayRate = 0.02;
        viewModel.StepsPerTick = 3;
        viewModel.AntMaximumLife = 750;

        Assert.That(viewModel.Colony.PheromoneDepositAmount, Is.EqualTo(4.5f));
        Assert.That(viewModel.Colony.TurnChance, Is.EqualTo(0.75));
        Assert.That(viewModel.Colony.MaximumRandomTurnRadians, Is.EqualTo(35 * Math.PI / 180).Within(0.0001));
        Assert.That(viewModel.Colony.FoodTrailIgnoreChance, Is.EqualTo(0.18).Within(0.0001));
        Assert.That(viewModel.Colony.AntMaximumLife, Is.EqualTo(750));
        Assert.That(viewModel.PheromoneDecayRate, Is.EqualTo(0.02));
        Assert.That(viewModel.StepsPerTick, Is.EqualTo(3));
    }

    [Test]
    public void AntCountChangeRestartsColony()
    {
        using var viewModel = new MainWindowViewModel(false);

        viewModel.AntCount = 75;

        Assert.That(viewModel.Colony.Ants, Has.Count.EqualTo(75));
    }

    [Test]
    public void FoodSourceCountChangeRestartsWorld()
    {
        using var viewModel = new MainWindowViewModel(false);

        viewModel.FoodSourceCount = 4;

        Assert.That(viewModel.World.FoodSources, Has.Count.EqualTo(4 * World.FoodBlobsPerSource));
    }

    [Test]
    public void FoodCountersResetWhenGameRestarts()
    {
        using var viewModel = new MainWindowViewModel(false);
        viewModel.SeedText = "1";
        viewModel.AntCount = 1;
        viewModel.TurnChance = 0;
        viewModel.PheromoneDecayRate = 0.012;
        viewModel.RestartGame();
        var ant = viewModel.Colony.Ants[0];
        var food = viewModel.World.FoodSources[0];
        ant.Position = food.Position;
        ant.State = AntState.Searching;
        viewModel.Tick();
        ant = viewModel.Colony.Ants[0];
        ant.Position = viewModel.World.NestPosition;
        ant.State = AntState.Returning;
        viewModel.Tick();

        Assert.That(viewModel.FoodFoundCount, Is.GreaterThan(0));
        Assert.That(viewModel.FoodReturnedHomeCount, Is.GreaterThan(0));

        viewModel.RestartGame();

        Assert.That(viewModel.FoodFoundCount, Is.Zero);
        Assert.That(viewModel.FoodReturnedHomeCount, Is.Zero);
    }

    private sealed class TestAppSettings : IAppSettings
    {
        public string SeedText { get; set; } = "1";

        public double PheromoneDecayRate { get; set; } = AppSettings.DefaultPheromoneDecayRate;

        public double PheromoneDepositAmount { get; set; } = AppSettings.DefaultPheromoneDepositAmount;

        public double TurnChance { get; set; } = AppSettings.DefaultTurnChance;

        public double RandomTurnDegrees { get; set; } = AppSettings.DefaultRandomTurnDegrees;

        public double FoodTrailIgnoreChance { get; set; } = AppSettings.DefaultFoodTrailIgnoreChance;

        public int StepsPerTick { get; set; } = AppSettings.DefaultStepsPerTick;

        public int AntCount { get; set; } = AppSettings.DefaultAntCount;

        public int AntMaximumLife { get; set; } = AppSettings.DefaultAntMaximumLife;

        public int FoodSourceCount { get; set; } = AppSettings.DefaultFoodSourceCount;

        public bool IsHomePheromoneVisible { get; set; } = AppSettings.DefaultIsHomePheromoneVisible;

        public bool IsFoodPheromoneVisible { get; set; } = AppSettings.DefaultIsFoodPheromoneVisible;

        public bool IsSensorOverlayVisible { get; set; } = AppSettings.DefaultIsSensorOverlayVisible;

        public bool IsDisposed { get; private set; }

        public void Dispose() =>
            IsDisposed = true;
    }
}
