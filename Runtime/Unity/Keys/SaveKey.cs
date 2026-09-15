using System;
using UnityEngine;

namespace Deucarian.Persistence
{
    /// <summary>A declared Save identity. Reuse a named definition or select it in the Inspector.</summary>
    [Serializable]
    public class SaveKey<TData> : ISaveKey<TData>, IEquatable<SaveKey<TData>>
    {
        [SerializeField] private string definitionId;

        /// <summary>For central definition sets and generated declarations; ordinary callers reuse those keys.</summary>
        protected SaveKey(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
                throw new ArgumentException("A SaveKey definition needs a non-empty stable ID without surrounding whitespace.", nameof(id));
            definitionId = id;
        }

        public string Id => !string.IsNullOrWhiteSpace(definitionId) ? definitionId :
            throw new InvalidOperationException("No SaveKey is selected. Select an existing definition in the Inspector or assign a named key from a SaveKeySet declaration.");
        public bool Equals(SaveKey<TData> other) => other != null && string.Equals(definitionId, other.definitionId, StringComparison.Ordinal);
        public override bool Equals(object other) => other is SaveKey<TData> key && Equals(key);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(definitionId ?? string.Empty);
        public override string ToString() => definitionId ?? string.Empty;
    }
}
