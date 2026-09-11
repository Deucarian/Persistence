using System;
using System.Linq;
using Deucarian.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Persistence.Tests
{
    public sealed class SaveKeySerializationTests
    {
        [Test]
        public void GenericKeySerializesAndPickerOnlyOffersTheMatchingDataType()
        {
            var original = ScriptableObject.CreateInstance<SaveKeyCarrier>();
            var restored = ScriptableObject.CreateInstance<SaveKeyCarrier>();
            try
            {
                original.Save = SerializationSaveKeys.Settings;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), restored);
                Assert.That(restored.Save, Is.EqualTo(SerializationSaveKeys.Settings));
                using (var serialized = new SerializedObject(restored))
                {
                    var field = serialized.FindProperty(nameof(SaveKeyCarrier.Save));
                    Assert.That(field.FindPropertyRelative("definitionId"), Is.Not.Null);
                    Assert.That(DeucarianKeyPropertyType.Resolve(field), Is.EqualTo(typeof(SaveKey<SerializationSettings>)));
                }
                var choices = DeucarianKeyChoices.Read(typeof(SaveKey<SerializationSettings>), typeof(SaveKeySetAttribute), includeEditorDefinitions: true);
                Assert.That(choices.Any(c => c.Id == "serialization.settings"), Is.True);
                Assert.That(choices.Any(c => c.Id == "serialization.other"), Is.False);
                Assert.That(DeucarianKeyChoices.Read(typeof(SaveKey<SerializationSettings>), typeof(SaveKeySetAttribute)), Is.Empty,
                    "Definitions compiled only into test assemblies must never be offered to player components.");
            }
            finally { UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(restored); }
        }
    }

    public sealed class SaveKeyCarrier : ScriptableObject { public SaveKey<SerializationSettings> Save; }
    [Serializable] public sealed class SerializationSettings { public float Volume; }

    [SaveKeySet]
    public static class SerializationSaveKeys
    {
        public static SaveKey<SerializationSettings> Settings => new SettingsKey();
        public static SaveKey<string> Other => new OtherKey();
        private sealed class SettingsKey : SaveKey<SerializationSettings> { public SettingsKey() : base("serialization.settings") { } }
        private sealed class OtherKey : SaveKey<string> { public OtherKey() : base("serialization.other") { } }
    }
}
