namespace Biz.Bizadm.S2Protocol.NET.Protocols;

public sealed class UnsupportedProtocolException(int baseBuild)
    : Exception($"No protocol is available for base build {baseBuild}.")
{
    public int BaseBuild { get; } = baseBuild;
}
