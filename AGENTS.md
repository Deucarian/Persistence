# Deucarian Persistence Agent Notes

Package ID: `com.deucarian.persistence`
Repository: `Deucarian/Persistence`

Follow the canonical Deucarian governance docs in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/main/ARCHITECTURE.md), especially capability ownership and dependency rules.

## Ownership

This package owns:

- Versioned local document persistence, explicit migrations, validation before save/after load, file envelopes, checksums for accidental corruption, atomic local writes, rolling backups, recovery, and persistence result models.

Registered capabilities:
- None.

This package must not own:

- Cloud sync, encryption, anti-cheat, monetization, account/session behavior, game progression rules, currencies, combat, encounters, offline rewards, UI, scene discovery, or a global save singleton.

## Dependencies

Allowed dependency shape:

- May depend on Unity's Newtonsoft Json package for serialization support.
- Pure runtime assembly keeps `noEngineReferences` enabled; Unity adapter owns Unity-specific path behavior.

Required dependencies and why:

- `com.unity.nuget.newtonsoft-json`: JSON serialization used by persistence document storage.

Optional/version-defined dependencies:

- None.

Architecture exceptions:

- None.

## Policies

- Keep save semantics explicit through document definitions, slots, serializers, storage, clocks, and path providers.
- Do not add cloud/store/session/gameplay responsibilities to this package.
- Checksums are accidental-corruption checks only, not tamper protection.
- Logging: Do not introduce direct Unity Debug calls.
- Testing: Keep migrations, validation, write atomicity, backup, recovery, serialization, cancellation, and Unity path adapter behavior covered by tests.

## Validation

Run the shared validator before committing:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Also run existing repository tests when changing code or asmdefs. Documentation-only updates should still run `git diff --check`.

## Codex Guidance

- Inspect current files before changing anything.
- Work on `develop`; do not edit or merge `main` unless the task is promotion-only.
- Do not edit `Library/PackageCache`.
- Do not guess package versions or dependency versions.
- Do not add package dependencies casually; update asmdefs, `package.json`, `deucarian-package.json`, Package Registry, and fallback catalogs together when a dependency is truly required.
- Do not create local copies of shared helpers.
- Keep commits focused and report exactly what changed and what was validated.

