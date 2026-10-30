namespace Biz.Bizadm.S2Protocol.NET.Decoding;

public sealed class CorruptedReplayException(string message) : Exception(message);
