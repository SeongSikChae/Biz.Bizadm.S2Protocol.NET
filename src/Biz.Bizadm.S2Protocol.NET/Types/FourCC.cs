using System.Text;

namespace Biz.Bizadm.S2Protocol.NET;

public readonly record struct FourCC(uint Value)
{
    public override string ToString() =>
        Encoding.ASCII.GetString([(byte)(Value >> 24), (byte)(Value >> 16), (byte)(Value >> 8), (byte)Value]);
}
