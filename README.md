# Deucarian Persistence

Generic local persistence for Unity applications and games.

Package ID: `com.deucarian.persistence`

Namespace: `Deucarian.Persistence`

## Scope

This package provides:

- versioned document saves
- explicit migrations
- validation before save and after load
- local file envelopes with accidental-corruption checksums
- same-directory temporary writes
- bounded rolling backups
- corrupted-primary recovery
- explicit load and write result types
- cancellation-aware asynchronous APIs
- dependency injection through serializer, storage, clock, and path-provider interfaces

It does not provide cloud sync, encryption, anti-cheat, monetization, account/session behavior, game progression rules, currencies, combat, encounters, offline rewards, UI, scene discovery, or a global save singleton.

Checksums are only for accidental corruption detection. They are not protection against save editing or malicious tampering.

## Install Locally

```json
{
  "dependencies": {
    "com.deucarian.persistence": "file:C:/Repositories/Deucarian/Persistence"
  },
  "testables": [
    "com.deucarian.persistence"
  ]
}
```

Remote publication is deferred until a real repository exists.

## Assemblies

- `Deucarian.Persistence`: no Unity engine references, depends on `Newtonsoft.Json`.
- `Deucarian.Persistence.Unity`: Unity path-provider adapter. Construct `UnityPersistentDataPathProvider` on the main thread.

## Basic Usage

Create explicit DTOs with stable field semantics, define a `DocumentDefinition<T>`, then use `PersistenceService`.

```csharp
var definition = new DocumentDefinition<SettingsDto>(
    new DocumentId("settings"),
    new SchemaVersion(1),
    () => new SettingsDto(),
    new DelegateDocumentValidator<SettingsDto>(dto =>
        dto.Volume >= 0f && dto.Volume <= 1f
            ? ValidationResult.Success()
            : ValidationResult.Failure("Volume is out of range.")));

var storage = new FileTextStorage(new UnityPersistentDataPathProvider());
using var service = new PersistenceService(storage);
WriteResult save = await service.SaveAsync(definition, settings, SaveSlotId.Default);
LoadResult<SettingsDto> load = await service.LoadAsync(definition, SaveSlotId.Default);
```

See `Samples~/VersionedLocalSave` for settings, profile, and run-resume documents.

## Install

Stable:

```json
"com.deucarian.persistence": "https://github.com/Deucarian/Persistence.git#main"
```

Development:

```json
"com.deucarian.persistence": "https://github.com/Deucarian/Persistence.git#develop"
```

Use `#main` for stable package consumption and `#develop` when testing active package work.

## When To Use This

Use this package when you need Versioned local document persistence with migrations, validation, atomic file writes, backups, and recovery.

Do not use this package to take ownership of capabilities outside its `AGENTS.md` boundary. Reusable behavior should stay with the package that owns that capability in the Package Registry governance docs.

## Quick Start

1. Install the package through Deucarian Package Installer or Unity Package Manager using the URL above.
2. Let Unity finish resolving packages and compiling assemblies.
3. Import the `Versioned Local Save` sample if you want a working reference scene or setup.
4. Start from the package README sections above and the public runtime/editor APIs in this repository.

## Troubleshooting

- Package does not resolve: confirm the stable or development Git URL matches the Package Registry entry and that required Deucarian dependencies are installed.
- Unity compile errors after install: let Package Manager finish resolving dependencies, then check asmdef references against `package.json` dependencies.
- Behavior appears to belong in another package: consult `AGENTS.md` and the Package Registry governance docs before moving or duplicating code.

## Validation

Run the shared package validator from this repository root:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Documentation-only updates should still pass:

```powershell
git diff --check
```

## License

MIT. See `LICENSE.md`.
