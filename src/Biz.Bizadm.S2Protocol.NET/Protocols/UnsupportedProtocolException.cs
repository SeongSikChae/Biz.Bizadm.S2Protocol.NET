namespace Biz.Bizadm.S2Protocol.NET.Protocols;

/// <summary>Thrown when no protocol is available for a base build.</summary>
/// <param name="baseBuild">The requested base build.</param>
public sealed class UnsupportedProtocolException(int baseBuild)
    : Exception($"No protocol is available for base build {baseBuild}.")
{
    /// <summary>The requested base build.</summary>
    public int BaseBuild { get; } = baseBuild;
}
