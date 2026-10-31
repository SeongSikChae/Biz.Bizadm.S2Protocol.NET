namespace Biz.Bizadm.S2Protocol.NET.Decoding;

/// <summary>Thrown when replay data is truncated or does not match the protocol.</summary>
/// <param name="message">The error message.</param>
public sealed class CorruptedReplayException(string message) : Exception(message);
