namespace Biz.Bizadm.S2Protocol.NET;

public readonly record struct FourCC(uint Value)
{
    // 하위 네임스페이스 Biz.Bizadm.S2Protocol.NET.Encoding이 System.Text.Encoding을 가리므로 정규화한다.
    public override string ToString() =>
        System.Text.Encoding.ASCII.GetString([(byte)(Value >> 24), (byte)(Value >> 16), (byte)(Value >> 8), (byte)Value]);
}
