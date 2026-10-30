namespace Biz.Bizadm.S2Protocol.NET;

// 태그는 이벤트의 32비트 부호 없는 필드(생성 타입에서 long)와 같은 범위다.
public static class UnitTag
{
    public static long Create(int index, int recycle) => ((long)index << 18) + recycle;

    public static int Index(long tag) => (int)((tag >> 18) & 0x3fff);

    public static int Recycle(long tag) => (int)(tag & 0x3ffff);
}
