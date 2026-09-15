using System;
using Deucarian.Editor;
using UnityEditor;

namespace Deucarian.Persistence.Editor
{
    [CustomPropertyDrawer(typeof(SaveKey<>), true)]
    public sealed class SaveKeyDrawer : DeucarianKeyDrawer
    {
        public override Type KeyType => typeof(SaveKey<>);
        public override Type DefinitionSetAttribute => typeof(SaveKeySetAttribute);
        public override string SetupHint => "Select an existing SaveKey; declare reusable keys once in a [SaveKeySet] class.";
    }
}
