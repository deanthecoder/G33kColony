# Agents.md

## Summary

G33kColony is a C# Avalonia app for exploring ant colony simulation, emergent behaviour, and pheromone-driven swarm intelligence.

The project currently starts as a small application shell with a blank main window and shared About dialog. Keep the early architecture simple while the simulation model, renderer, and controls take shape.

## Key Guidance

- Prefer focused changes that move one simulation, UI, or packaging concern at a time.
- Reuse `DTC.Core` helpers wherever they fit.
- If a helper is generic and reusable, consider whether it belongs in `DTC.Core`.
- Prefer proven code from other DeanTheCoder repos before adding new local infrastructure.
- Update unit tests for meaningful behaviour changes.
- If user-facing behaviour changes noticeably, update the README too.

## Coding Preferences

- Commit messages must start with `Feature:`, `Fix:`, or `Other:`, use sentence case, and end with a full stop.
- Prefer method names without underscores.
- Async methods should end with `Async` unless a framework contract prevents that.
- Reserve `m_` for fields only, not locals or parameters.
- Validate public method arguments appropriately.
- New public classes and public methods should usually have unit tests unless that would be disproportionate.
- Small incidental cleanups such as removing unused `using`s can be folded into nearby work.

## Project Structure

Prefer these folders:

- `Models`
- `Services`
- `Views`
- `ViewModels`

General intent:

- `Models` hold simulation data structures and settings.
- `Services` contain reusable simulation, persistence, and infrastructure logic.
- `Views` and `ViewModels` keep the Avalonia UI and MVVM state clean.

## Documentation And Style

- Add class-level XML docs to reusable public types.
- Prefer readable code over clever code.
- Avoid unnecessary abstractions.
- Keep files focused.
- Prefer simple MVVM boundaries.
