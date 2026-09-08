using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler shim for `init` accessors and `record` types.
///
/// .NET Standard 2.1 — Unity's compile target — does not define this, and the compiler
/// requires it for any init-only setter. Domain carries its own copy; the type is
/// `internal`, so it is not shared across assemblies and each one that uses records needs
/// its own. This file exists so Gameplay can declare record types at all.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
