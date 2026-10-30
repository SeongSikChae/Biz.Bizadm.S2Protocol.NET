namespace Biz.Bizadm.S2Protocol.NET.Tests;

[TestClass]
public sealed class UnitTagTests
{
    [TestMethod]
    public void Create_MatchesPythonFormula()
    {
        Assert.AreEqual(4194305L, UnitTag.Create(16, 1));
    }

    [TestMethod]
    public void Create_MaxIndex_StaysPositive()
    {
        Assert.AreEqual(0xFFFFFFFFL, UnitTag.Create(0x3fff, 0x3ffff));
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(16, 1)]
    [DataRow(0x3fff, 0x3ffff)]
    [DataRow(1234, 56789)]
    public void IndexAndRecycle_RoundTrip(int index, int recycle)
    {
        long tag = UnitTag.Create(index, recycle);

        Assert.AreEqual(index, UnitTag.Index(tag));
        Assert.AreEqual(recycle, UnitTag.Recycle(tag));
    }
}
