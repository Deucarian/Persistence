# ADR 0001 - Persistence Design

Date: 2026-06-22

## Status

Accepted for Phase 1B.

## Decision

Use a generic document persistence package with a pure C# runtime assembly and a separate Unity path adapter assembly.

## Serializer Strategy

Persistence exposes `IPersistenceSerializer` and ships `NewtonsoftPersistenceSerializer` as the default. Unity's supported `com.unity.nuget.newtonsoft-json` package is already used by existing Deucarian packages and the donor project.

Options evaluated:

- Serializer abstraction only: flexible, but makes every consumer build a serializer before saving.
- Unity Newtonsoft JSON: supports dictionaries and explicit DTOs, works in Unity, already established in Deucarian packages.
- Framework serializers: less consistent across Unity profiles and AOT targets.
- `JsonUtility`: fast, but weak for dictionaries, top-level flexible envelopes, and migration payload handling.

The default workflow avoids reflection-heavy polymorphic behavior: `TypeNameHandling.None`, explicit DTOs, and stable fields.

## Envelope Strategy

Files contain a JSON envelope with format marker, document id, schema version, UTC timestamp, payload JSON text, and SHA-256 checksum over the payload.

Checksums detect accidental corruption only. They are not encryption, anti-cheat, or tamper protection.

## Migration Model

Migrations implement `IDocumentMigration`, declare document id, source version, destination version, and transform payload JSON text. `DocumentMigrationSet` rejects duplicate source steps and invalid backwards edges, detects missing paths, and rejects unsupported newer saves.

The original save is not replaced during load migration. A migrated document is returned only after the complete migration and validation succeed.

## Atomic Replacement And Backups

The file flow serializes and validates before touching the primary, writes a unique same-directory temporary file, flushes and closes it, rotates the previous primary to a bounded backup, moves the temporary file into the primary name, and restores the newest backup if replacement fails after rotation.

`File.Replace` is intentionally not required because behavior differs across Unity-supported platforms. The package uses a portable move/delete fallback and documents that platform atomicity is best effort.

## Async And Concurrency

Public APIs are cancellation-aware `Task` methods. Per-document locks are owned by each `PersistenceService` instance. There are no static mutable lock dictionaries. Multiple writes to the same document serialize; different documents may proceed independently depending on the storage backend.

## Unity Path Integration

`UnityPersistentDataPathProvider` captures `Application.persistentDataPath` on construction. Consumers should construct it on the main thread; background file operations only use the captured string.

## AOT And IL2CPP

The default serializer uses explicit DTOs and no runtime type-name polymorphism. Consumers targeting IL2CPP should preserve DTO constructors and fields according to their normal Unity stripping policy. The package does not claim blanket AOT compatibility beyond the tested DTO workflow.

## Gameplay Foundation Dependency

Persistence does not depend on `com.deucarian.gameplay-foundation`. Document and slot identifiers are storage concepts, not gameplay content identifiers, and adding Gameplay Foundation would make Persistence more game-specific without improving cohesion.

## Logging And Diagnostics

Core correctness does not depend on global logging or diagnostics. Future optional integration can expose a diagnostics provider, but the save/load result model already carries explicit reasons and messages.
