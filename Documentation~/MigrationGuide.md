# Migration Guide

1. Define DTOs for each schema version that needs explicit migration.
2. Create one `IDocumentMigration` per version step.
3. Keep migrations deterministic and side-effect free.
4. Validate the final current-version DTO.
5. Do not rely on new DTO fields receiving defaults as the whole migration strategy.

Example:

```csharp
var migrations = new DocumentMigrationSet(new IDocumentMigration[]
{
    new DelegateDocumentMigration(
        new DocumentId("profile"),
        new SchemaVersion(0),
        new SchemaVersion(1),
        (payload, serializer) =>
        {
            ProfileV0 old = serializer.Deserialize<ProfileV0>(payload);
            return serializer.Serialize(new ProfileV1
            {
                PlayerName = old.DisplayName,
                Experience = old.LegacyXp
            });
        })
});
```

Saves created by newer unsupported schemas fail with `LoadOutcome.UnsupportedNewerSchema`. Missing steps fail with `LoadOutcome.MissingMigration`.
