# PhotoFlow

Desktop application (C#/.NET + Avalonia) to import photos from SD cards, cull
them automatically, tag them, recognize faces and race numbers, and search large
photo archives — with local AI models (ONNX Runtime), no cloud, no telemetry.

> Status: **early development** (phase 0 — repository setup and lab).
> Nothing here is usable yet.

## Principles

- **No data loss**: the import source is never written to, RAW files are never
  modified (XMP sidecars only), no file is ever deleted automatically.
- **User actions win**: manual tags and decisions are never overwritten by
  automatic processes.
- **Standard interoperability**: data lives in a local catalog and is always
  exported as standard XMP.

The full specification (Italian) is in [`docs/spec.md`](docs/spec.md).

## Requirements

- .NET SDK 10 (pinned in `global.json`)
- Windows 10/11 x64 (primary target); macOS and Linux possible
- Optional, for the Python lab in `tools/lab`: [uv](https://docs.astral.sh/uv/)
  and an NVIDIA GPU

## Build and test

```bash
dotnet build
dotnet test --filter "Category=Unit|Category=Integration|Category=Scenario"
```

Tests that need local data (`Parity`, `Quality`, `Performance`) read the folder
pointed to by the `PHOTOFLOW_TESTDATA` environment variable and are skipped when
it is not set.

## Contributing: enable the pre-commit hook

The repository is public: personal photos must never be committed. After cloning,
enable the versioned hooks once:

```bash
git config core.hooksPath .githooks
```

The hook rejects images and RAW files (by extension and by file signature)
outside `tests/data/public/`, which only contains files with a verified free
license listed in `tests/data/public/LICENSES.md`. CI runs the same check.

## Models

Model files are never stored in this repository. `models/manifest.json` lists
each model with its source URL, SHA-256, size and license; models are downloaded
on first use after the license has been shown.

## License

Apache License 2.0 — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).
