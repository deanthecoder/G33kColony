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

namespace G33kColony.Tests;

public class SimulationModelTests
{
    [Test]
    public void WorldUsesFixedDefaultSize()
    {
        var world = new World();

        Assert.That(world.Width, Is.EqualTo(640));
        Assert.That(world.Height, Is.EqualTo(480));
        Assert.That(world.Contains(world.NestPosition), Is.True);
        Assert.That(world.NestPosition.X, Is.GreaterThanOrEqualTo(Colony.NestRadius));
        Assert.That(world.NestPosition.X, Is.LessThanOrEqualTo(world.Width - 1 - Colony.NestRadius));
        Assert.That(world.NestPosition.Y, Is.GreaterThanOrEqualTo(Colony.NestRadius));
        Assert.That(world.NestPosition.Y, Is.LessThanOrEqualTo(world.Height - 1 - Colony.NestRadius));
        Assert.That(world.FoodSources, Has.Count.EqualTo(World.FoodBlobsPerSource));
    }

    [Test]
    public void WorldCreatesRequestedFoodBlobVicinity()
    {
        var world = new World(640, 480, 4, 12);

        Assert.That(world.FoodSources, Has.Count.EqualTo(4 * World.FoodBlobsPerSource));
        Assert.That(world.FoodSources, Is.All.Matches<FoodSource>(source => world.Contains(source.Position)));
        Assert.That(world.FoodSources, Is.All.Matches<FoodSource>(source => source.Radius <= 3));

        foreach (var source in world.FoodSources)
        {
            var hasNearbyFood = false;
            foreach (var otherSource in world.FoodSources)
            {
                if (ReferenceEquals(source, otherSource))
                    continue;

                if (source.Position.DistanceSquared(otherSource.Position) <= 20 * 20)
                {
                    hasNearbyFood = true;
                    break;
                }
            }

            Assert.That(hasNearbyFood, Is.True, "Food blob has no nearby blob in its vicinity.");
        }
    }

    [Test]
    public void PheromoneFieldStoresAndEvaporatesBlobs()
    {
        var pheromones = new PheromoneField();

        pheromones.Add(new WorldPoint(5, 5), 4, 10);
        var beforeEvaporation = pheromones.SampleTotal(new WorldPoint(5, 5), 4);
        pheromones.Evaporate(0.75f);
        var afterEvaporation = pheromones.SampleTotal(new WorldPoint(5, 5), 4);

        Assert.That(afterEvaporation, Is.GreaterThan(0));
        Assert.That(afterEvaporation, Is.LessThan(beforeEvaporation));
        Assert.That(pheromones.SampleTotal(new WorldPoint(50, 50), 4), Is.Zero);
    }

    [Test]
    public void AntPositionStaysInsideWorldAfterColonyTick()
    {
        var world = new World(80, 60);
        var colony = new Colony(world, 25, 7);

        for (var i = 0; i < 120; i++)
            colony.Tick();

        Assert.That(colony.Ants, Has.Count.EqualTo(25));
        Assert.That(colony.Ants, Is.All.Matches<Ant>(ant => !ant.IsAlive || world.Contains(ant.Position)));
    }

    [Test]
    public void SmallRandomTurnUpdatesHeadingWithoutSnappingToGrid()
    {
        var ant = new Ant(WorldPoint.Zero, 1, 0);

        ant.Turn(Colony.DefaultMaximumRandomTurnRadians / 2);

        Assert.That(ant.DirectionX, Is.GreaterThan(0.9));
        Assert.That(ant.DirectionY, Is.GreaterThan(0));
    }

    [Test]
    public void SearchingAntSteersTowardFoodScent()
    {
        var world = new World(80, 80, 0, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var ant = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        world.FoodPheromones.Add(new WorldPoint(48, 31), 8, 10);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.GreaterThan(0));
        Assert.That(Math.Abs(ant.DirectionY), Is.GreaterThan(0.1));
    }

    [Test]
    public void SearchingAntKeepsCloseToTrailWhenFoodScentIsStrong()
    {
        var world = new World(80, 80, 0, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 1
        };
        var ant = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        world.FoodPheromones.Add(new WorldPoint(48, 31), 8, 10);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.GreaterThan(0));
        Assert.That(Math.Abs(ant.DirectionY), Is.GreaterThan(0.2));
    }

    [Test]
    public void SearchingAntHeadsTowardDetectedFood()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 1
        };
        var sensorOffsetX = Colony.SensorDistance * Math.Cos(Colony.SensorAngleRadians);
        var sensorOffsetY = Colony.SensorDistance * Math.Sin(Colony.SensorAngleRadians);
        var food = world.FoodSources.First(source => world.Contains(source.Position.WithDelta(-sensorOffsetX, -sensorOffsetY)));
        foreach (var source in world.FoodSources)
        {
            if (!ReferenceEquals(source, food))
                Assert.That(source.TryConsume(source.Position), Is.True);
        }

        var ant = colony.AddAnt(food.Position.WithDelta(-sensorOffsetX, -sensorOffsetY), 1, 0);

        colony.Tick();

        var directionToFoodX = food.Position.X - ant.Position.X;
        var directionToFoodY = food.Position.Y - ant.Position.Y;
        var dot = ant.DirectionX * directionToFoodX + ant.DirectionY * directionToFoodY;
        Assert.That(dot, Is.GreaterThan(0));
    }

    [Test]
    public void SearchingAntIgnoresHomeScent()
    {
        var world = new World(80, 80, 0, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var ant = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        world.HomePheromones.Add(new WorldPoint(48, 31), 8, 10);

        colony.Tick();

        Assert.That(ant.DirectionX, Is.GreaterThan(0.9));
        Assert.That(Math.Abs(ant.DirectionY), Is.LessThan(0.5));
    }

    [Test]
    public void ReturningAntSteersTowardHomeScent()
    {
        var world = new World(120, 120);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var ant = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        ant.State = AntState.Returning;
        world.HomePheromones.Add(new WorldPoint(48, 31), 8, 10);

        colony.Tick();

        Assert.That(ant.DirectionY, Is.LessThan(0));
    }

    [Test]
    public void ReturningAntKeepsCloseToHomeScentWhenStrong()
    {
        var world = new World(120, 120);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 1
        };
        var ant = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        ant.State = AntState.Returning;
        world.HomePheromones.Add(new WorldPoint(48, 31), 8, 10);

        colony.Tick();

        Assert.That(ant.DirectionY, Is.LessThan(0));
    }

    [Test]
    public void ReturningAntHeadsTowardDetectedHome()
    {
        var world = new World(160, 160, 0, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var horizontalOffset = world.NestPosition.X + Colony.NestRadius + Colony.SensorDistance + 2 < world.Width
            ? Colony.NestRadius + Colony.SensorDistance + 2
            : -(Colony.NestRadius + Colony.SensorDistance + 2);
        var ant = colony.AddAnt(world.Clamp(world.NestPosition.WithDelta(horizontalOffset, 0)), horizontalOffset > 0 ? -1 : 1, 0);
        ant.State = AntState.Returning;

        colony.Tick();

        var toNestX = world.NestPosition.X - ant.Position.X;
        var toNestY = world.NestPosition.Y - ant.Position.Y;
        var dotToNest = ant.DirectionX * toNestX + ant.DirectionY * toNestY;

        Assert.That(ant.State, Is.EqualTo(AntState.Returning));
        Assert.That(dotToNest, Is.GreaterThan(0));
    }

    [Test]
    public void ReturningAntIgnoresFoodScent()
    {
        var worldWithFoodScent = new World(240, 240, 0, 12);
        var colonyWithFoodScent = new Colony(worldWithFoodScent, 0, 7)
        {
            TurnChance = 0
        };
        var horizontalOffset = worldWithFoodScent.NestPosition.X + 100 < worldWithFoodScent.Width ? 100 : -100;
        var start = worldWithFoodScent.Clamp(worldWithFoodScent.NestPosition.WithDelta(horizontalOffset, 0));
        var headingX = horizontalOffset > 0 ? 1 : -1;
        var antWithFoodScent = colonyWithFoodScent.AddAnt(start, headingX, 0);
        antWithFoodScent.State = AntState.Returning;
        worldWithFoodScent.FoodPheromones.Add(antWithFoodScent.Position.WithDelta(8, -9), 8, 10);

        var worldWithoutFoodScent = new World(240, 240, 0, 12);
        var colonyWithoutFoodScent = new Colony(worldWithoutFoodScent, 0, 7)
        {
            TurnChance = 0
        };
        var antWithoutFoodScent = colonyWithoutFoodScent.AddAnt(start, headingX, 0);
        antWithoutFoodScent.State = AntState.Returning;

        colonyWithFoodScent.Tick();
        colonyWithoutFoodScent.Tick();

        Assert.That(antWithFoodScent.Position.X, Is.EqualTo(antWithoutFoodScent.Position.X).Within(0.0001));
        Assert.That(antWithFoodScent.Position.Y, Is.EqualTo(antWithoutFoodScent.Position.Y).Within(0.0001));
    }

    [Test]
    public void ReturningAntDoesNotTurnAwayFromHomeWhenNearSearchingAnt()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var horizontalOffset = world.NestPosition.X + 30 < world.Width ? 30 : -30;
        var returningAnt = colony.AddAnt(world.Clamp(world.NestPosition.WithDelta(horizontalOffset, 0)), horizontalOffset > 0 ? -1 : 1, 0);
        returningAnt.State = AntState.Returning;
        var blockerOffset = world.NestPosition.X + 24 < world.Width ? 24 : -24;
        colony.AddAnt(world.Clamp(world.NestPosition.WithDelta(blockerOffset, 0)), blockerOffset > 0 ? 1 : -1, 0);
        var distanceBefore = returningAnt.Position.DistanceSquared(world.NestPosition);

        colony.Tick();

        Assert.That(returningAnt.Position.DistanceSquared(world.NestPosition), Is.LessThan(distanceBefore));
    }

    [Test]
    public void SpawnedAntHeadsTowardNearbyFoodScent()
    {
        var world = new World(80, 80, 1, 12);
        world.FoodPheromones.Add(world.NestPosition.WithDelta(Colony.SensorDistance, 0), 8, 10);

        var colony = new Colony(world, 1, 7);
        var ant = colony.Ants[0];

        Assert.That(ant.Position.DistanceSquared(world.NestPosition), Is.EqualTo(Colony.NestRadius * Colony.NestRadius).Within(1));
        var target = world.NestPosition.WithDelta(Colony.SensorDistance, 0);
        var directionToTargetX = target.X - ant.Position.X;
        var directionToTargetY = target.Y - ant.Position.Y;
        var dot = ant.DirectionX * directionToTargetX + ant.DirectionY * directionToTargetY;
        Assert.That(dot, Is.GreaterThan(0));
    }

    [Test]
    public void AntLifeResetsAndFoodDepletesWhenFoodIsFound()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 3,
            TurnChance = 0
        };
        var food = world.FoodSources[0];
        var ant = colony.AddAnt(food.Position.WithDelta(-food.Radius - 0.5, 0), 1, 0);
        var foodBefore = food.RemainingAmount;

        colony.Tick();

        Assert.That(ant.State, Is.EqualTo(AntState.Returning));
        Assert.That(ant.LifeRemaining, Is.EqualTo(3));
        Assert.That(food.RemainingAmount, Is.EqualTo(foodBefore - 1));
    }

    [Test]
    public void AntFindsFoodJustOutsideSmallBlobRadius()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var food = world.FoodSources[0];
        var ant = colony.AddAnt(food.Position.WithDelta(-food.Radius - 2.5, 0), 1, 0);

        colony.Tick();

        Assert.That(ant.State, Is.EqualTo(AntState.Returning));
    }

    [Test]
    public void AntRespawnsAtNestWhenLifeExpires()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 1,
            TurnChance = 0
        };
        var ant = colony.AddAnt(world.NestPosition, 1, 0);

        colony.Tick();

        Assert.That(ant.IsAlive, Is.True);
        for (var i = 0; i < 500 && !ant.IsAlive; i++)
            colony.Tick();

        Assert.That(ant.IsAlive, Is.True);
        Assert.That(ant.Position.DistanceSquared(world.NestPosition), Is.EqualTo(Colony.NestRadius * Colony.NestRadius).Within(4));
        Assert.That(ant.State, Is.EqualTo(AntState.Searching));
        Assert.That(ant.LifeRemaining, Is.GreaterThan(0));
    }

    [Test]
    public void ReturningAntRespawnsFreshWhenItReachesHome()
    {
        var world = new World(80, 80, 1, 12);
        world.FoodPheromones.Add(world.NestPosition.WithDelta(Colony.SensorDistance, 0), 8, 10);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 9,
            TurnChance = 0
        };
        var ant = colony.AddAnt(world.NestPosition.WithDelta(0, 1), 0, -1);
        ant.State = AntState.Returning;

        colony.Tick();

        Assert.That(ant.IsAlive, Is.True);
        Assert.That(ant.Position.DistanceSquared(world.NestPosition), Is.EqualTo(Colony.NestRadius * Colony.NestRadius).Within(4));
        Assert.That(ant.State, Is.EqualTo(AntState.Searching));
        Assert.That(ant.LifeRemaining, Is.GreaterThan(0));
        Assert.That(colony.FoodReturnedHomeCount, Is.EqualTo(1));
    }

    [Test]
    public void AntMovesAroundOccupiedTarget()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var movingAnt = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        var blockingAnt = colony.AddAnt(new WorldPoint(41.4, 40), 1, 0);

        colony.Tick();

        Assert.That(movingAnt.Position, Is.Not.EqualTo(new WorldPoint(41.4, 40)));
        Assert.That(movingAnt.Position, Is.Not.EqualTo(new WorldPoint(40, 40)));
        Assert.That(movingAnt.Position.DistanceSquared(blockingAnt.Position), Is.GreaterThan(0));
    }

    [Test]
    public void OverlappingAntsSteerApart()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var movingAnt = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        var blockingAnt = colony.AddAnt(new WorldPoint(40, 40), 1, 0);

        colony.Tick();

        Assert.That(movingAnt.Position.DistanceSquared(blockingAnt.Position), Is.GreaterThan(0));
    }

    [Test]
    public void AntStaysStillWhenAllNearbyMovesAreOccupied()
    {
        var world = new World(80, 80, 0, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 10,
            TurnChance = 0
        };
        var movingAnt = colony.AddAnt(new WorldPoint(40, 40), 1, 0);
        for (var i = 0; i < 8; i++)
        {
            var heading = i * Math.PI / 4;
            colony.AddAnt(
                movingAnt.Position.WithDelta(Math.Cos(heading) * 1.4, Math.Sin(heading) * 1.4),
                1,
                0);
        }

        colony.Tick();

        Assert.That(world.Contains(movingAnt.Position), Is.True);
        Assert.That(movingAnt.LifeRemaining, Is.EqualTo(movingAnt.MaximumLife - 1));
    }

    [Test]
    public void AntRespawnsNearNestWhenNestIsBlocked()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 1,
            TurnChance = 0
        };
        var expiringAnt = colony.AddAnt(world.NestPosition, 1, 0);

        colony.Tick();
        for (var i = 0; i < 500 && !expiringAnt.IsAlive; i++)
            colony.Tick();

        Assert.That(expiringAnt.IsAlive, Is.True);
        Assert.That(expiringAnt.Position, Is.Not.EqualTo(world.NestPosition));
        Assert.That(expiringAnt.Position.DistanceSquared(world.NestPosition), Is.EqualTo(Colony.NestRadius * Colony.NestRadius).Within(4));
        Assert.That(expiringAnt.LifeRemaining, Is.GreaterThan(0));
    }

    [Test]
    public void AntsSpawnNearHomeWhenHomeIsBlocked()
    {
        var world = new World(640, 480, 1, 12);
        var colony = new Colony(world, 2, 7)
        {
            TurnChance = 0
        };

        Assert.That(colony.Ants, Has.Count.EqualTo(2));
        Assert.That(colony.Ants.Count(ant => ant.IsAlive), Is.EqualTo(2));
        Assert.That(colony.Ants[0].Position.DistanceSquared(world.NestPosition), Is.EqualTo(Colony.NestRadius * Colony.NestRadius).Within(4));
        Assert.That(colony.Ants[1].Position.DistanceSquared(world.NestPosition), Is.EqualTo(Colony.NestRadius * Colony.NestRadius).Within(4));
    }

    [Test]
    public void DeadAntDoesNotRespawnWhenFoodIsGone()
    {
        var world = new World(80, 80, 1, 12);
        var colony = new Colony(world, 0, 7)
        {
            AntMaximumLife = 1,
            TurnChance = 0
        };
        var ant = colony.AddAnt(new WorldPoint(10, 10), 1, 0);
        foreach (var food in world.FoodSources)
        {
            while (food.TryConsume(food.Position))
            {
            }
        }

        colony.Tick();
        colony.Tick();

        Assert.That(world.HasFoodRemaining, Is.False);
        Assert.That(ant.IsAlive, Is.False);
    }

    [Test]
    public void AntReturnsHomeAfterFindingFoodByFollowingHomeBlobs()
    {
        var world = new World(80, 80);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var food = world.FoodSources[0];
        var ant = colony.AddAnt(world.NestPosition, food.Position.X - world.NestPosition.X, food.Position.Y - world.NestPosition.Y);

        for (var i = 0; i < 120 && ant.State == AntState.Searching; i++)
        {
            colony.Tick();
            world.Tick(0.995f);
        }

        Assert.That(ant.State, Is.EqualTo(AntState.Returning));

        for (var i = 0; i < 180 && ant.State == AntState.Returning; i++)
        {
            colony.Tick();
            world.Tick(0.995f);
        }

        Assert.That(ant.State, Is.EqualTo(AntState.Searching));
        Assert.That(ant.Position.DistanceSquared(world.NestPosition), Is.LessThanOrEqualTo(Colony.NestRadius * Colony.NestRadius + 0.001));
    }

    [Test]
    public void NewAntFollowsFoodTrailAfterFirstAntReturnsHome()
    {
        var world = new World(80, 80);
        var colony = new Colony(world, 0, 7)
        {
            TurnChance = 0
        };
        var food = world.FoodSources[0];
        var firstAnt = colony.AddAnt(world.NestPosition, food.Position.X - world.NestPosition.X, food.Position.Y - world.NestPosition.Y);

        for (var i = 0; i < 120 && firstAnt.State == AntState.Searching; i++)
        {
            colony.Tick();
            world.Tick(0.995f);
        }

        Assert.That(firstAnt.State, Is.EqualTo(AntState.Returning));

        for (var i = 0; i < 180 && firstAnt.State == AntState.Returning; i++)
        {
            colony.Tick();
            world.Tick(0.995f);
        }

        Assert.That(firstAnt.State, Is.EqualTo(AntState.Searching));
        Assert.That(world.FoodPheromones.SampleTotal(world.NestPosition, Colony.NestRadius * 2), Is.GreaterThan(0));

        var trailCell = FindFoodTrailCellClosestToNest(world);
        var (directionX, directionY) = CreateUnitDirection(trailCell, food.Position);
        var spawnPosition = world.Clamp(trailCell.WithDelta(
            -directionX * Colony.AntRadius,
            -directionY * Colony.AntRadius));
        var secondAnt = colony.AddAnt(spawnPosition, directionX, directionY);
        for (var i = 0; i < 120 && secondAnt.State == AntState.Searching; i++)
        {
            colony.Tick();
            world.Tick(0.995f);
        }

        Assert.That(secondAnt.State, Is.EqualTo(AntState.Returning));
        Assert.That(colony.FoodFoundCount, Is.GreaterThanOrEqualTo(2));
    }

    private static WorldPoint FindFoodTrailCellClosestToNest(World world)
    {
        var result = WorldPoint.Zero;
        var found = false;
        var bestDistanceSquared = double.MaxValue;
        var field = world.FoodPheromones;

        for (var y = 0; y < field.GridHeight; y++)
        for (var x = 0; x < field.GridWidth; x++)
        {
            if (field.GetCellStrength(x, y) <= 0)
                continue;

            var position = field.GetCellCenter(x, y);
            if (world.IsFood(position) ||
                position.DistanceSquared(world.NestPosition) <= Colony.NestRadius * Colony.NestRadius)
            {
                continue;
            }

            var distanceSquared = position.DistanceSquared(world.NestPosition);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            found = true;
            result = position;
            bestDistanceSquared = distanceSquared;
        }

        Assert.That(found, Is.True);
        return result;
    }

    private static (double X, double Y) CreateUnitDirection(WorldPoint from, WorldPoint to)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        Assert.That(length, Is.GreaterThan(0));
        return (deltaX / length, deltaY / length);
    }
}
