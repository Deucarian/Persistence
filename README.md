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
