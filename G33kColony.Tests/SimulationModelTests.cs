// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using G33kColony.Models;

namespace G33kColony.Tests;

public class SimulationModelTests
{
    [Test]
    public void WorldUsesFixedDefaultSize()
    {
        var world = new World();

        Assert.That(world.Width, Is.EqualTo(640));
        Assert.That(world.Height, Is.EqualTo(480));
        Assert.That(world.NestPosition, Is.EqualTo(new IntPoint(320, 240)));
        Assert.That(world.FoodSources, Has.Count.EqualTo(1));
    }

    [Test]
    public void WorldCreatesRequestedFoodSources()
    {
        var world = new World(640, 480, 4, 12);

        Assert.That(world.FoodSources, Has.Count.EqualTo(4));
        Assert.That(world.FoodSources, Is.All.Matches<FoodSource>(source => world.Contains(source.Position)));

        // Verify distance between food sources and nest.
        foreach (var source in world.FoodSources)
        {
            var diameterSquared = Math.Pow(source.Radius * 2, 2);
            Assert.That(source.Position.DistanceSquared(world.NestPosition), Is.AtLeast(diameterSquared), "Food source too close to nest.");
        }

        // Verify distance between food sources.
        for (var i = 0; i < world.FoodSources.Count; i++)
        {
            for (var j = i + 1; j < world.FoodSources.Count; j++)
            {
                var s1 = world.FoodSources[i];
                var s2 = world.FoodSources[j];
                var diameterSquared = Math.Pow(s1.Radius * 2, 2);
                Assert.That(s1.Position.DistanceSquared(s2.Position), Is.AtLeast(diameterSquared), "Food sources too close to each other.");
            }
        }
    }

    [Test]
    public void PheromoneDataStoresAndEvaporatesCellValues()
    {
        var pheromones = new PheromoneData(3, 2);

        pheromones.Add(1, 1, 10);
        pheromones.Evaporate(0.75f);

        Assert.That(pheromones[1, 1], Is.EqualTo(7.5f));
        Assert.That(pheromones[0, 0], Is.Zero);
    }

    [Test]
    public void AntPositionStaysInsideWorldAfterColonyTick()
    {
        var world = new World(8, 6);
        var colony = new Colony(world, 25, 7);

        for (var i = 0; i < 40; i++)
            colony.Tick();

        Assert.That(colony.Ants, Has.Count.EqualTo(25));
        Assert.That(colony.Ants, Is.All.Matches<Ant>(ant => world.Contains(ant.Position)));
    }

    [Test]
    public void SmallRandomTurnDoesNotImmediatelyJumpCellDirection()
    {
        var ant = new Ant(IntPoint.Zero, 1, 0);

        ant.Turn(Colony.DefaultMaximumRandomTurnRadians / 2);

        Assert.That(ant.DirectionX, Is.EqualTo(1));
        Assert.That(ant.DirectionY, Is.Zero);
    }

    [Test]
    public void SearchingAntSteersTowardFoodScent()
    {
        var world = new World(40, 40);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.SetDirection(1, 0);
        world.FoodPheromones.Add(ant.Position.X + 2, ant.Position.Y, 10);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.EqualTo(1));
    }

    [Test]
    public void SearchingAntPrefersStrongestFoodScent()
    {
        var world = new World(40, 40);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.SetDirection(1, 0);
        world.FoodPheromones.Add(ant.Position.X + 1, ant.Position.Y, 10);
        world.FoodPheromones.Add(ant.Position.X + 2, ant.Position.Y + 1, 9);
        world.FoodPheromones.Add(ant.Position.X + 2, ant.Position.Y - 1, 9);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.EqualTo(1));
    }

    [Test]
    public void SearchingAntIgnoresHomeScent()
    {
        var world = new World(40, 40);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.SetDirection(1, 0);
        world.HomePheromones.Add(ant.Position.X + 2, ant.Position.Y, 10);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.EqualTo(1));
        Assert.That(ant.DirectionY, Is.Zero);
    }

    [Test]
    public void ReturningAntSteersTowardHomeScent()
    {
        var world = new World(40, 40);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.MoveTo(new IntPoint(24, 20));
        ant.SetDirection(-1, 0);
        ant.SetState(AntState.Returning);
        world.HomePheromones.Add(ant.Position.X - 2, ant.Position.Y, 10);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.EqualTo(-1));
    }

    [Test]
    public void ReturningAntPrefersStrongestHomeScent()
    {
        var world = new World(40, 40);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.MoveTo(new IntPoint(24, 20));
        ant.SetDirection(-1, 0);
        ant.SetState(AntState.Returning);
        world.HomePheromones.Add(ant.Position.X - 1, ant.Position.Y, 10);
        world.HomePheromones.Add(ant.Position.X - 2, ant.Position.Y + 1, 9);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.EqualTo(-1));
    }

    [Test]
    public void ReturningAntInsideFoodMovesTowardHome()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        var food = world.FoodSources[0];
        ant.MoveTo(food.Position);
        ant.SetDirection(
            Math.Sign(food.Position.X - world.NestPosition.X),
            Math.Sign(food.Position.Y - world.NestPosition.Y));
        ant.SetState(AntState.Returning);
        var distanceBefore = ant.Position.DistanceSquared(world.NestPosition);

        colony.Tick();

        Assert.That(ant.State, Is.EqualTo(AntState.Returning));
        Assert.That(world.IsFood(ant.Position), Is.True);
        Assert.That(ant.Position.DistanceSquared(world.NestPosition), Is.LessThan(distanceBefore));
    }

    [Test]
    public void AntRespawnsAtNestWhenLifeExpires()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 1, 7)
        {
            AntMaximumLife = 1,
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.MoveTo(world.NestPosition);
        ant.SetDirection(1, 0);

        colony.Tick();

        Assert.That(ant.Position, Is.EqualTo(world.NestPosition));
        Assert.That(ant.State, Is.EqualTo(AntState.Searching));
        Assert.That(ant.LifeRemaining, Is.EqualTo(1));
    }

    [Test]
    public void AntWaitsToRespawnUntilNestIsClear()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 1,
            TurnChance = 0
        };
        var expiringAnt = colony.AddAnt(world.NestPosition, 1, 0);
        var blockingAnt = colony.AddAnt(world.NestPosition.WithDelta(1, 0), 1, 0);

        colony.Tick();

        Assert.That(expiringAnt.IsAlive, Is.False);

        blockingAnt.MoveTo(world.NestPosition.WithDelta(10, 0));
        colony.Tick();

        Assert.That(expiringAnt.IsAlive, Is.True);
        Assert.That(expiringAnt.Position, Is.EqualTo(world.NestPosition));
        Assert.That(expiringAnt.LifeRemaining, Is.EqualTo(1));
    }

    [Test]
    public void AntLifeResetsWhenFoodIsFound()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 1, 7)
        {
            AntMaximumLife = 3,
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        var food = world.FoodSources[0];
        ant.MoveTo(new IntPoint(food.Position.X - food.Radius - 1, food.Position.Y));
        ant.SetDirection(1, 0);

        colony.Tick();

        Assert.That(ant.State, Is.EqualTo(AntState.Returning));
        Assert.That(ant.LifeRemaining, Is.EqualTo(3));
    }

    [Test]
    public void AntMovesToNeighbouringCellWhenPreferredCellIsOccupied()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 10,
            TurnChance = 0
        };
        var movingAnt = colony.AddAnt(new IntPoint(40, 40), 1, 0);
        colony.AddAnt(new IntPoint(41, 40), 1, 0);

        colony.Tick();

        Assert.That(movingAnt.Position, Is.Not.EqualTo(new IntPoint(41, 40)));
        Assert.That(movingAnt.Position, Is.Not.EqualTo(new IntPoint(40, 40)));
        Assert.That(movingAnt.Position.DistanceSquared(new IntPoint(40, 40)), Is.LessThanOrEqualTo(2));
        Assert.That(movingAnt.LifeRemaining, Is.EqualTo(9));
    }

    [Test]
    public void AntStaysStillAndAgesWhenSurrounded()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 10,
            TurnChance = 0
        };
        var movingAnt = colony.AddAnt(new IntPoint(40, 40), 1, 0);

        for (var y = 39; y <= 41; y++)
        for (var x = 39; x <= 41; x++)
        {
            var position = new IntPoint(x, y);
            if (position != movingAnt.Position)
                colony.AddAnt(position, 1, 0);
        }

        colony.Tick();

        Assert.That(movingAnt.Position, Is.EqualTo(new IntPoint(40, 40)));
        Assert.That(movingAnt.LifeRemaining, Is.EqualTo(9));
    }

    [Test]
    public void AntReturnsHomeAfterFindingFood()
    {
        var world = new World(80, 80, 1, 1);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var ant = colony.Ants[0];
        ant.SetDirection(1, 1);

        var foundFood = false;

        for (var i = 0; i < 400; i++)
        {
            colony.Tick();
            world.Tick(0.988f);

            if (ant.State == AntState.Returning)
                foundFood = true;

            if (foundFood && ant.State == AntState.Searching)
                break;
        }

        Assert.That(foundFood, Is.True);
        Assert.That(ant.State, Is.EqualTo(AntState.Searching));
        Assert.That(ant.Position.DistanceSquared(world.NestPosition), Is.LessThan(12 * 12));
    }

    [Test]
    public void NewAntFollowsFoodTrailAfterFirstAntReturnsHome()
    {
        var world = new World(80, 80, 1, 1);
        var colony = new Colony(world, 1, 7)
        {
            TurnChance = 0
        };
        var firstAnt = colony.Ants[0];
        firstAnt.SetDirection(1, 1);
        var firstAntFoundFood = false;

        for (var i = 0; i < 400; i++)
        {
            colony.Tick();
            world.Tick(0.988f);

            if (firstAnt.State == AntState.Returning)
                firstAntFoundFood = true;

            if (firstAntFoundFood && firstAnt.State == AntState.Searching)
                break;
        }

        Assert.That(firstAntFoundFood, Is.True);
        Assert.That(firstAnt.State, Is.EqualTo(AntState.Searching));

        var trailCell = FindFoodTrailCellClosestToNest(world);
        var (spawnPosition, directionX, directionY) = FindAdjacentEmptyCell(world, trailCell);
        var secondAnt = colony.AddAnt(spawnPosition, directionX, directionY);

        for (var i = 0; i < 160; i++)
        {
            colony.Tick();
            world.Tick(0.988f);

            if (secondAnt.State == AntState.Returning)
                break;
        }

        Assert.That(colony.Ants, Has.Count.EqualTo(2));
        Assert.That(world.IsFood(secondAnt.Position), Is.True);
        Assert.That(secondAnt.State, Is.EqualTo(AntState.Returning));
    }

    private static IntPoint FindFoodTrailCellClosestToNest(World world)
    {
        var result = world.NestPosition;
        var bestDistanceSquared = double.MaxValue;

        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var position = new IntPoint(x, y);
            if (world.FoodPheromones[x, y] <= 0 || world.IsFood(position))
                continue;

            var distanceSquared = position.DistanceSquared(world.NestPosition);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            result = position;
            bestDistanceSquared = distanceSquared;
        }

        Assert.That(bestDistanceSquared, Is.LessThan(double.MaxValue));
        return result;
    }

    private static (IntPoint Position, int DirectionX, int DirectionY) FindAdjacentEmptyCell(World world, IntPoint trailCell)
    {
        var directionX = Math.Sign(trailCell.X - world.NestPosition.X);
        var directionY = Math.Sign(trailCell.Y - world.NestPosition.Y);
        var preferredPosition = trailCell.WithDelta(-directionX, -directionY);
        if (world.Contains(preferredPosition) &&
            !world.IsFood(preferredPosition) &&
            world.FoodPheromones[preferredPosition.X, preferredPosition.Y] <= 0)
        {
            return (preferredPosition, directionX, directionY);
        }

        for (var y = trailCell.Y - 1; y <= trailCell.Y + 1; y++)
        for (var x = trailCell.X - 1; x <= trailCell.X + 1; x++)
        {
            var position = new IntPoint(x, y);
            if (position == trailCell || !world.Contains(position) || world.IsFood(position))
                continue;

            if (world.FoodPheromones[x, y] <= 0)
                return (position, Math.Sign(trailCell.X - x), Math.Sign(trailCell.Y - y));
        }

        Assert.Fail($"Could not find an empty cell next to food trail cell {trailCell}.");
        return (trailCell, 1, 0);
    }
}
