using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deucarian.Persistence.Unity;

namespace Deucarian.Persistence.Samples
{
    public static class VersionedLocalSaveSample
    {
        public static async Task<string> RunAsync()
        {
            var storage = new FileTextStorage(new UnityPersistentDataPathProvider());
            using var service = new PersistenceService(storage);

            DocumentDefinition<SettingsDto> settings = new DocumentDefinition<SettingsDto>(
                new DocumentId("sample-settings"),
                new SchemaVersion(1),
                () => new SettingsDto(),
                new DelegateDocumentValidator<SettingsDto>(dto => dto.Volume >= 0f && dto.Volume <= 1f
                    ? ValidationResult.Success()
                    : ValidationResult.Failure("Volume must be between 0 and 1.")));

            DocumentDefinition<ProfileDto> profile = new DocumentDefinition<ProfileDto>(
                new DocumentId("sample-profile"),
                new SchemaVersion(1),
                () => new ProfileDto(),
                new DelegateDocumentValidator<ProfileDto>(dto => string.IsNullOrWhiteSpace(dto.DisplayName)
                    ? ValidationResult.Failure("Display name is required.")
                    : ValidationResult.Success()));

            DocumentDefinition<RunResumeDto> runResume = new DocumentDefinition<RunResumeDto>(
                new DocumentId("sample-run-resume"),
                new SchemaVersion(1),
                () => new RunResumeDto(),
                new DelegateDocumentValidator<RunResumeDto>(dto => string.IsNullOrWhiteSpace(dto.RunId)
                    ? ValidationResult.Failure("Run id is required.")
                    : ValidationResult.Success()));

            await service.SaveAsync(settings, new SettingsDto { Volume = 0.8f, Locale = "en-US" }, SaveSlotId.Default);
            await service.SaveAsync(profile, new ProfileDto { DisplayName = "Player", Counters = new Dictionary<string, int> { { "runs", 1 } } }, SaveSlotId.Default);
            await service.SaveAsync(runResume, new RunResumeDto { RunId = "interrupted-run", Tick = 120 }, SaveSlotId.Default);

            LoadResult<SettingsDto> loadedSettings = await service.LoadAsync(settings, SaveSlotId.Default);
            WriteResult resetRun = await service.DeleteAsync(new DocumentLocation(new DocumentId("sample-run-resume"), SaveSlotId.Default));

            return $"settings={loadedSettings.Outcome}; runReset={resetRun.Outcome}";
        }

        public sealed class SettingsDto
        {
            public float Volume { get; set; } = 1f;
            public string Locale { get; set; } = "en-US";
        }

        public sealed class ProfileDto
        {
            public string DisplayName { get; set; } = "Player";
            public Dictionary<string, int> Counters { get; set; } = new Dictionary<string, int>();
        }

        public sealed class RunResumeDto
        {
            public string RunId { get; set; } = "none";
            public int Tick { get; set; }
        }
    }
}
