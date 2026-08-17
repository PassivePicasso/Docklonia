namespace Docklonia.Serialization;

/// <summary>
/// Schema version and the compatibility policy that governs it (§8).
/// </summary>
/// <remarks>
/// <para><b>Backward compatibility.</b> A document written by an older library
/// version always loads. Fields added later are absent, and every added field
/// carries a default that reproduces the older behaviour, so an old document
/// deserializes to the layout it described rather than to a partial one.</para>
///
/// <para><b>Forward compatibility.</b> A document written by a <i>newer</i>
/// library version is rejected rather than partially applied: an unknown node
/// kind or an unknown structural field cannot be silently dropped without
/// changing the tree, and a partially-applied layout would then be written back
/// over the user's real one. Unknown <i>properties</i> within a known node kind
/// are ignored, which is what lets a same-version document survive a patch
/// release that adds cosmetic state.</para>
///
/// <para>Callers that would rather start clean than fail should catch
/// <see cref="LayoutFormatException"/> and assign a fresh layout.</para>
/// </remarks>
public static class LayoutSchema
{
    /// <summary>Version stamped into every document this library writes.</summary>
    public const int Version = 1;

    /// <summary>Oldest version this library can still read.</summary>
    public const int MinimumSupportedVersion = 1;
}

/// <summary>Thrown when a layout document cannot be read as written.</summary>
public sealed class LayoutFormatException : Exception
{
    public LayoutFormatException(string message) : base(message)
    {
    }

    public LayoutFormatException(string message, Exception inner) : base(message, inner)
    {
    }
}
