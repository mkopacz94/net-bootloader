namespace NetBootloader.Core.HexFile;

/// <summary>A contiguous run of bytes at a given address, parsed from an Intel HEX file.</summary>
public sealed record HexSegment(uint Address, byte[] Data);
