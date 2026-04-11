// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

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
        Assert.That(viewModel.World.FoodSources, Has.Count.EqualTo(1));
        Assert.That(viewModel.FoodSourceCount, Is.EqualTo(1));
        Assert.That(viewModel.IsHomePheromoneVisible, Is.True);
        Assert.That(viewModel.IsFoodPheromoneVisible, Is.True);
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
        viewModel.PheromoneDecayRate = 0.02;
        viewModel.StepsPerTick = 3;
        viewModel.AntMaximumLife = 750;

        Assert.That(viewModel.Colony.PheromoneDepositAmount, Is.EqualTo(4.5f));
        Assert.That(viewModel.Colony.TurnChance, Is.EqualTo(0.75));
        Assert.That(viewModel.Colony.MaximumRandomTurnRadians, Is.EqualTo(35 * Math.PI / 180).Within(0.0001));
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

        Assert.That(viewModel.World.FoodSources, Has.Count.EqualTo(4));
    }

    [Test]
    public void FoodCountersResetWhenGameRestarts()
    {
        using var viewModel = new MainWindowViewModel(false);
        viewModel.SeedText = "1";
        viewModel.AntCount = 25;
        viewModel.TurnChance = 0;
        viewModel.PheromoneDecayRate = 0.012;
        viewModel.RestartGame();
        viewModel.Colony.Ants[0].SetDirection(1, 1);

        for (var i = 0; i < 400 && viewModel.FoodReturnedHomeCount == 0; i++)
            viewModel.Tick();

        Assert.That(viewModel.FoodFoundCount, Is.GreaterThan(0));
        Assert.That(viewModel.FoodReturnedHomeCount, Is.GreaterThan(0));

        viewModel.RestartGame();

        Assert.That(viewModel.FoodFoundCount, Is.Zero);
        Assert.That(viewModel.FoodReturnedHomeCount, Is.Zero);
    }
}
