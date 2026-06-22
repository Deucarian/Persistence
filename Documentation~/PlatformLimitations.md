# Platform Limitations

- File replacement is best-effort atomic. The package avoids assuming `File.Replace` works identically on all Unity-supported platforms.
- The tested fallback uses same-directory temporary files plus move/delete behavior.
- Mobile lifecycle interruption was represented by deterministic fault-injecting storage, not by killing a real mobile application.
- `UnityPersistentDataPathProvider` captures `Application.persistentDataPath`; construct it on Unity's main thread.
- No cloud sync is included.
- No encryption is included.
- Checksums are not anti-cheat or tamper protection.
- Offline reward calculation belongs to a future progression or idle package.
- IL2CPP consumers should use explicit DTOs and normal Unity preservation policy for serialized members.
