namespace Biz.Bizadm.S2Protocol.NET.Protocols;

// 생성된 빌드별 프로토콜 목록. 목록 자체는 생성 파일(Generated/Versions/S2ProtocolRegistry.g.cs)의 Factories()가 준다.
// 프로토콜 인스턴스는 처음 요청할 때 만들고 재사용한다.

/// <summary>The generated protocols, one per s2protocol base build.</summary>
public static partial class S2ProtocolRegistry
{
    private static readonly SortedDictionary<int, Lazy<IS2Protocol>> s_protocols = new(
        Factories().ToDictionary(f => f.Build, f => new Lazy<IS2Protocol>(f.Create)));

    /// <summary>The generated base builds in ascending order.</summary>
    public static IReadOnlyList<int> Builds { get; } = [.. s_protocols.Keys];

    /// <summary>The protocol of the latest base build, used to decode replay headers.</summary>
    /// <exception cref="InvalidOperationException">No protocols are generated.</exception>
    public static IS2Protocol Latest => Builds.Count > 0
        ? s_protocols[Builds[^1]].Value
        : throw new InvalidOperationException("No protocols are generated.");

    /// <summary>Gets the protocol of a base build.</summary>
    /// <param name="baseBuild">The replay's base build (<c>IHeader.Version.BaseBuild</c>).</param>
    /// <param name="options">The fallback when no protocol exactly matches; by default none.</param>
    /// <returns>The protocol.</returns>
    /// <exception cref="UnsupportedProtocolException">No protocol matches and the fallback finds none.</exception>
    public static IS2Protocol Get(int baseBuild, ProtocolResolveOptions? options = null)
    {
        if (s_protocols.TryGetValue(baseBuild, out var exact))
            return exact.Value;

        switch (options?.Fallback ?? ProtocolFallback.None)
        {
            case ProtocolFallback.NearestLower:
                int lower = Builds.LastOrDefault(b => b < baseBuild);
                if (lower > 0)
                    return s_protocols[lower].Value;
                break;
            case ProtocolFallback.Latest when Builds.Count > 0:
                return Latest;
        }
        throw new UnsupportedProtocolException(baseBuild);
    }

    private static partial IReadOnlyList<(int Build, Func<IS2Protocol> Create)> Factories();
}
