using System;

namespace Deucarian.Persistence
{
    /// <summary>Marks an authoritative set of named SaveKey fields or properties for the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SaveKeySetAttribute : Attribute { }
}
