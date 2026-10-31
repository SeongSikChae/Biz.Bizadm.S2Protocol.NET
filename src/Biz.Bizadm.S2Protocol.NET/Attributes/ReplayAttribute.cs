namespace Biz.Bizadm.S2Protocol.NET.Attributes;

// replay.attributes.events의 항목 하나. RawValue는 파일의 4바이트 그대로이며(인코딩용),
// Value는 Python과 같이 역순으로 뒤집고 양쪽의 0 바이트를 제거한 값이다.
/// <summary>One entry of <c>replay.attributes.events</c>.</summary>
/// <param name="Namespace">The attribute namespace.</param>
/// <param name="AttrId">The attribute id (see <see cref="AttributeIds"/>).</param>
/// <param name="Scope">The scope (16 for the game, otherwise a player slot).</param>
/// <param name="RawValue">The four value bytes as stored, kept for encoding.</param>
public sealed record ReplayAttribute(uint Namespace, uint AttrId, byte Scope, byte[] RawValue)
{
    /// <summary>The value as s2protocol returns it: <see cref="RawValue"/> reversed, with zero bytes trimmed from both ends.</summary>
    public byte[] Value
    {
        get
        {
            var reversed = RawValue.Reverse().ToArray();
            int start = 0, end = reversed.Length;
            while (start < end && reversed[start] == 0) start++;
            while (end > start && reversed[end - 1] == 0) end--;
            return reversed[start..end];
        }
    }
}
