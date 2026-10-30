namespace Biz.Bizadm.S2Protocol.NET.Protocols;

// 정확히 일치하는 빌드가 없을 때의 동작. 기본은 None(예외)이며 Python versions.build()와 같다.
public enum ProtocolFallback
{
    None,
    NearestLower,
    Latest,
}

public sealed class ProtocolResolveOptions
{
    public ProtocolFallback Fallback { get; init; }
}
