namespace Biz.Bizadm.S2Protocol.NET.Tests;

[TestClass]
public sealed class FourCCTests
{
    [TestMethod]
    public void ToString_UsesBigEndianByteOrder()
    {
        var fourCC = new FourCC(0x7332_6D61);   // 's' '2' 'm' 'a'

        Assert.AreEqual("s2ma", fourCC.ToString());
    }
}
