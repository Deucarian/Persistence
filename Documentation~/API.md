# API

Namespace: `Deucarian.Persistence`

## Identity And Location

- `DocumentId`: safe document identifier.
- `SaveSlotId`: safe slot identifier.
- `DocumentLocation`: document plus slot.
- `SchemaVersion`: non-negative schema version.

Identifiers reject path traversal, rooted paths, directory separators, invalid filename characters, and whitespace-only values.

## Service

- `IPersistenceService`
- `PersistenceService`

Methods:

- `LoadAsync<T>(DocumentDefinition<T>, SaveSlotId, CancellationToken)`
- `SaveAsync<T>(DocumentDefinition<T>, T, SaveSlotId, CancellationToken)`
- `DeleteAsync(DocumentLocation, CancellationToken)`

## Definitions

- `DocumentDefinition<T>`
- `IDocumentValidator<T>`
- `DelegateDocumentValidator<T>`
- `ValidationResult`

## Results

- `LoadResult<T>`
- `WriteResult`
- `LoadOutcome`
- `WriteOutcome`
- `PersistenceFailureReason`
- `RecoverySource`

Consumers should branch on outcomes and failure reasons instead of parsing exception text.

## Serialization

- `IPersistenceSerializer`
- `NewtonsoftPersistenceSerializer`
- `SaveEnvelope`
- `SaveEnvelopeCodec`

The default serializer preserves nulls, supports dictionaries, and disables type-name polymorphism.

## Migration

- `IDocumentMigration`
- `DelegateDocumentMigration`
- `DocumentMigrationSet`

Migrations are explicit version-to-version transforms.

## Storage

- `ITextStorage`
- `FileTextStorage`
- `InMemoryTextStorage`
- `FaultInjectingTextStorage`

`InMemoryTextStorage` and `FaultInjectingTextStorage` are included for deterministic tests and compatibility harnesses.

## Unity

Namespace: `Deucarian.Persistence.Unity`

- `UnityPersistentDataPathProvider`

Construct on Unity's main thread and pass to `FileTextStorage`.
