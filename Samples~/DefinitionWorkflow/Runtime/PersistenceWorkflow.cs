using System;
using UnityEngine;
using Deucarian.Persistence;
namespace Deucarian.Persistence.Unity.Samples.DefinitionWorkflow
{
    /// <summary>Small caller example. The configured scene hosts own services and resource lifetimes.</summary>
    public sealed class PersistenceWorkflow : MonoBehaviour
    {
        [SerializeField] private SaveProfileHost host;
        [SerializeField] private SaveKey<SampleSettings> key = SampleSaveKeys.Settings;
        [SerializeField] private SampleSettingsTrigger trigger;
        private string status = "Ready. Choose an action below.";
        public string Status => status;
        public async void Save() { var result = await host.SaveAsync(key, new SampleSettings { Volume = 0.5f }); status = result.Succeeded ? "Saved in the sample's memory storage." : result.Message; }
        public async void Load() { var result = await host.LoadAsync(key); status = result.Succeeded ? "Loaded volume: " + result.Document.Volume : result.Message; }
        public void SaveComponent() { trigger.Save(); status = "Saved the component's settings through the same profile."; }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, Math.Min(540, Screen.width - 48), Screen.height - 48), GUI.skin.box);
            GUILayout.Label("Persistence — definition workflow");
            GUILayout.Label("Save definitions pair a typed document with its schema version and migration policy. This sample uses isolated memory storage; leaving Play mode resets it.");
            GUILayout.Space(12);
            if (GUILayout.Button("Save volume 0.5", GUILayout.Height(32))) { try { Save(); } catch (Exception error) { status = error.Message; } }
            if (GUILayout.Button("Load settings", GUILayout.Height(32))) { try { Load(); } catch (Exception error) { status = error.Message; } }
            if (GUILayout.Button("Save from component", GUILayout.Height(32))) { try { SaveComponent(); } catch (Exception error) { status = error.Message; } }
            GUILayout.Space(12);
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
