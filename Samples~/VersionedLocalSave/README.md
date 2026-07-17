# Versioned Local Save

This code sample defines settings, profile, and interrupted-run documents, writes them atomically beneath Unity's persistent-data path, loads the settings document, and explicitly deletes the run-resume document.

Await `VersionedLocalSaveSample.RunAsync()` from an application bootstrap or EditMode test. The returned text summarizes the load and reset outcomes.
