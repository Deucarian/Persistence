# Package Validation

Phase 1B validation uses Unity `6000.3.5f1`.

Clean validation project:

`C:\Repositories\Deucarian\Persistence-TestProject`

Local package reference:

```json
"com.deucarian.persistence": "file:C:/Repositories/Deucarian/Persistence"
```

Recorded commands and results are summarized in the coordinating Phase 1B report.

## Recorded Phase 1B Results

Import command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.5f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Repositories\Deucarian\Persistence-TestProject' -logFile 'C:\Repositories\Deucarian\Persistence-TestProject-import.log'
```

Initial import result: Unity returned `0` with no `error CS`, `Compilation failed`, or `Scripts have compiler errors` entries.

Sample import: `Samples~/VersionedLocalSave/VersionedLocalSaveSample.cs` was copied into the validation project's `Assets/Samples/VersionedLocalSave` folder and executed by a smoke test.

EditMode command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.5f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Repositories\Deucarian\Persistence-TestProject' -executeMethod BatchEditModeTestRunner.Run -batchTestResults 'C:\Repositories\Deucarian\Persistence-TestProject\Logs\batch-editmode-results.txt' -logFile 'C:\Repositories\Deucarian\Persistence-TestProject-batch-tests.log'
```

Result:

```text
result=Passed; passCount=25; failCount=0; skipCount=0; duration=0,716
```

Repeated command result:

```text
result=Passed; passCount=25; failCount=0; skipCount=0; duration=0,706
```

No PlayMode tests were added for Phase 1B. The only Unity-specific runtime behavior is `UnityPersistentDataPathProvider`, which captures `Application.persistentDataPath` on construction and is covered by compile/import plus the sample smoke test.
