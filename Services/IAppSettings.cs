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

namespace G33kColony.Services;

internal interface IAppSettings : IDisposable
{
    string SeedText { get; set; }

    double PheromoneDecayRate { get; set; }

    double PheromoneDepositAmount { get; set; }

    double TurnChance { get; set; }

    double RandomTurnDegrees { get; set; }

    int StepsPerTick { get; set; }

    int AntCount { get; set; }

    int AntMaximumLife { get; set; }

    int FoodSourceCount { get; set; }

    bool IsHomePheromoneVisible { get; set; }

    bool IsFoodPheromoneVisible { get; set; }

    bool IsSensorOverlayVisible { get; set; }
}
