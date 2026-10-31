namespace Biz.Bizadm.S2Protocol.NET;

/// <summary>A bit array field.</summary>
/// <param name="Length">Number of bits.</param>
/// <param name="Data">
/// The bits as a big-endian integer in <c>(Length + 7) / 8</c> bytes; unused high bits of the first byte are zero.
/// </param>
public sealed record S2BitArray(int Length, byte[] Data);
