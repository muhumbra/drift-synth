# Contributing

Thanks for your interest in Drift.

## Getting started

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`).
2. Clone the repository and build:

   ```bash
   dotnet build Drift.sln
   ```

3. Run the UI project:

   ```bash
   dotnet run --project Drift.Ui/Drift.Ui.csproj
   ```

## Project structure

- **`Drift.Engine`** — Real-time audio, MIDI, patches, effects. Keep the audio callback lean and allocation-aware.
- **`Drift.Ui`** — Avalonia front end and view models.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the pieces connect.

## Pull requests

- Keep changes focused; avoid drive-by refactors unrelated to the fix or feature.
- Match existing naming, formatting, and patterns in touched files.
- If you change user-visible behavior, update `README.md` or `CHANGELOG.md` as appropriate.

## Personal / local files

The `.private/` directory is **gitignored**. Use it for personal notes, scratch files, or anything you do not want in the public repository (for example a personal backlog or local experiments).

## Licensing

By contributing, you agree that your contributions will be licensed under the same terms as the project ([MIT](LICENSE)).
