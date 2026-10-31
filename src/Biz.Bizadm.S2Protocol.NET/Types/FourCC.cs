namespace Biz.Bizadm.S2Protocol.NET;

/// <summary>A four-character code stored as a big-endian 32-bit value.</summary>
/// <param name="Value">The four bytes, first character in the most significant byte.</param>
public readonly record struct FourCC(uint Value)
{
    /// <summary>Returns the four characters as ASCII text.</summary>
    // 하위 네임스페이스 Biz.Bizadm.S2Protocol.NET.Encoding이 System.Text.Encoding을 가리므로 정규화한다.
    public override string ToString() =>
        System.Text.Encoding.ASCII.GetString([(byte)(Value >> 24), (byte)(Value >> 16), (byte)(Value >> 8), (byte)Value]);
}
