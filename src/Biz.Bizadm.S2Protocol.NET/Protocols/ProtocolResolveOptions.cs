namespace Biz.Bizadm.S2Protocol.NET.Protocols;

/// <summary>What to use when no protocol exactly matches a base build.</summary>
// 정확히 일치하는 빌드가 없을 때의 동작. 기본은 None(예외)이며 Python versions.build()와 같다.
public enum ProtocolFallback
{
    /// <summary>Throw <see cref="UnsupportedProtocolException"/> (the s2protocol behavior).</summary>
    None,

    /// <summary>Use the nearest lower generated build.</summary>
    NearestLower,

    /// <summary>Use the latest generated build.</summary>
    Latest,
}

/// <summary>Options for <see cref="S2ProtocolRegistry.Get(int, ProtocolResolveOptions?)"/>.</summary>
public sealed class ProtocolResolveOptions
{
    /// <summary>The fallback when no protocol exactly matches. Default is <see cref="ProtocolFallback.None"/>.</summary>
    public ProtocolFallback Fallback { get; init; }
}
