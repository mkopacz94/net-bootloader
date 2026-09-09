namespace NetBootloader.Core.HexFile;

/// <summary>
/// Minimal Intel HEX (.hex) parser. Handles data records (00), end-of-file (01),
/// extended segment address (02) and extended linear address (04) records - which
/// covers the output of MPLAB X / XC16 for PIC24 and dsPIC33 targets, the devices
/// mcbootflash (and this port) target. Start segment/linear address records (03, 05)
/// are recognized and ignored, since they only matter for a CPU's own reset vector,
/// not for flashing.
/// </summary>
public static class IntelHexParser
{
    public static IReadOnlyList<HexSegment> Parse(string path)
    {
        using var reader = new StreamReader(path);
        return Parse(reader);
    }

    public static IReadOnlyList<HexSegment> Parse(TextReader reader)
    {
        var bytesByAddress = new SortedDictionary<uint, byte>();
        uint upperAddress = 0;
        var lineNumber = 0;
        var reachedEndOfFile = false;
        string? line;

        while (!reachedEndOfFile && (line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] != ':')
            {
                throw new FormatException($"Line {lineNumber}: expected a record starting with ':'.");
            }

            byte[] record;

            try
            {
                record = Convert.FromHexString(line.AsSpan(1));
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Line {lineNumber}: malformed hex data.", ex);
            }

            if (record.Length < 5)
            {
                throw new FormatException($"Line {lineNumber}: record too short.");
            }

            var byteCount = record[0];
            var recordAddress = (ushort)((record[1] << 8) | record[2]);
            var recordType = record[3];

            if (record.Length != byteCount + 5)
            {
                throw new FormatException($"Line {lineNumber}: byte count does not match record length.");
            }

            byte checksum = 0;
            foreach (var b in record)
            {
                checksum += b;
            }

            if (checksum != 0)
            {
                throw new FormatException($"Line {lineNumber}: checksum mismatch.");
            }

            switch (recordType)
            {
                case 0x00: // Data
                    var baseAddress = upperAddress + recordAddress;

                    for (var i = 0; i < byteCount; i++)
                    {
                        bytesByAddress[(uint)(baseAddress + i)] = record[4 + i];
                    }

                    break;

                case 0x01: // End of file
                    reachedEndOfFile = true;
                    break;

                case 0x02: // Extended segment address
                    var segment = (ushort)((record[4] << 8) | record[5]);
                    upperAddress = (uint)segment << 4;
                    break;

                case 0x04: // Extended linear address
                    var upper16 = (ushort)((record[4] << 8) | record[5]);
                    upperAddress = (uint)upper16 << 16;
                    break;

                case 0x03: // Start segment address - irrelevant for flashing
                case 0x05: // Start linear address - irrelevant for flashing
                    break;

                default:
                    throw new FormatException($"Line {lineNumber}: unsupported record type {recordType:X2}.");
            }
        }

        return MergeIntoSegments(bytesByAddress);
    }

    private static List<HexSegment> MergeIntoSegments(SortedDictionary<uint, byte> bytesByAddress)
    {
        var segments = new List<HexSegment>();

        if (bytesByAddress.Count == 0)
        {
            return segments;
        }

        uint segmentStart = 0;
        uint expectedNext = 0;
        var buffer = new List<byte>();
        var first = true;

        foreach (var (address, value) in bytesByAddress)
        {
            if (first)
            {
                segmentStart = address;
                first = false;
            }
            else if (address != expectedNext)
            {
                segments.Add(new HexSegment(segmentStart, buffer.ToArray()));
                buffer.Clear();
                segmentStart = address;
            }

            buffer.Add(value);
            expectedNext = address + 1;
        }

        segments.Add(new HexSegment(segmentStart, buffer.ToArray()));
        return segments;
    }
}
