using System.Buffers.Binary;
using System.Text;

namespace Logitech.RawPeripheralAccess.ProtocolDiscovery.Fingerprints;

internal sealed class BusSwitchStaircaseV1 : IProtocolFingerprint
{
    public string Id => "logi-bus-switch-staircase-v1";

    private static ReadOnlySpan<byte> MouseDescriptor => [
        0x05, 0x01, 0x09, 0x02, 0xA1, 0x01, 0x09, 0x01, 0xA1, 0x00, 0x05, 0x09,
        0x19, 0x01, 0x29, 0x10, 0x15, 0x00, 0x25, 0x01, 0x95, 0x10, 0x75, 0x01,
        0x81, 0x02, 0x05, 0x01, 0x16, 0x01, 0x80, 0x26, 0xFF, 0x7F, 0x75, 0x10,
        0x95, 0x02, 0x09, 0x30, 0x09, 0x31, 0x81, 0x06, 0x15, 0x81, 0x25, 0x7F,
        0x75, 0x08, 0x95, 0x01, 0x09, 0x38, 0x81, 0x06, 0x05, 0x0C, 0x0A, 0x38,
        0x02, 0x95, 0x01, 0x81, 0x06, 0xC0, 0xC0
    ];

    private static readonly byte[] MouseIdentity =
        Encoding.Unicode.GetBytes("LGHUBDevice\\VID_046D&PID_C231");

    public bool TryMatch(ReadOnlySpan<byte> image, PeImage pe, out FingerprintMatch match)
    {
        match = default;
        var descriptorOffset = FindUnique(image, MouseDescriptor);
        if (descriptorOffset < 0 || FindUnique(image, MouseIdentity) < 0)
            return false;

        var foundOffset = -1;
        var foundBase = 0u;
        foreach (var section in pe.Sections)
        {
            if (!section.IsExecutable || section.RawSize < PatternLength)
                continue;

            var sectionBytes = image.Slice(section.RawOffset, section.RawSize);
            var searchOffset = 0;
            while (searchOffset <= sectionBytes.Length - PatternLength)
            {
                var anchor = sectionBytes[searchOffset..].IndexOf((byte)0x81);
                if (anchor < 0)
                    break;

                var candidate = searchOffset + anchor;
                if (MatchesDispatch(sectionBytes[candidate..]))
                {
                    var ioctlBase = BinaryPrimitives.ReadUInt32LittleEndian(
                        sectionBytes.Slice(candidate + 2, sizeof(uint)));
                    if (ValidateIoctlFamily(ioctlBase))
                    {
                        if (foundOffset >= 0)
                            return false;
                        foundOffset = section.RawOffset + candidate;
                        foundBase = ioctlBase;
                    }
                }

                searchOffset = candidate + 1;
            }
        }

        if (foundOffset < 0)
            return false;

        match = new FingerprintMatch(
            foundOffset,
            descriptorOffset,
            checked(foundBase + 0x04),
            checked(foundBase + 0x10),
            checked(foundBase + 0x40));
        return true;
    }

    private const int PatternLength = 67;

    private static bool MatchesDispatch(ReadOnlySpan<byte> value)
    {
        if (value.Length < PatternLength)
            return false;

        // Relative branch displacements are deliberately ignored.
        return value[0] == 0x81 && value[1] == 0xEA &&
               value[6] == 0x0F && value[7] == 0x84 &&
               value[12] == 0x83 && value[13] == 0xEA && value[14] == 0x04 &&
               value[15] == 0x0F && value[16] == 0x84 &&
               value[21] == 0xB8 && value[22] == 0x08 &&
               value[23] == 0x00 && value[24] == 0x00 && value[25] == 0x00 &&
               value[26] == 0x2B && value[27] == 0xD0 &&
               value[28] == 0x0F && value[29] == 0x84 &&
               value[34] == 0x83 && value[35] == 0xEA && value[36] == 0x04 &&
               value[37] == 0x0F && value[38] == 0x84 &&
               value[43] == 0x83 && value[44] == 0xEA && value[45] == 0x2C &&
               value[46] == 0x0F && value[47] == 0x84 &&
               value[52] == 0x83 && value[53] == 0xEA && value[54] == 0x04 &&
               value[55] == 0x74 &&
               value[57] == 0x83 && value[58] == 0xEA && value[59] == 0x04 &&
               value[60] == 0x74 &&
               value[62] == 0x83 && value[63] == 0xFA && value[64] == 0x04 &&
               value[65] == 0x0F && value[66] == 0x85;
    }

    private static bool ValidateIoctlFamily(uint ioctlBase)
    {
        Span<uint> codes = stackalloc uint[5]
        {
            ioctlBase + 0x04,
            ioctlBase + 0x0C,
            ioctlBase + 0x10,
            ioctlBase + 0x3C,
            ioctlBase + 0x40
        };

        var deviceType = codes[0] >> 16;
        if (deviceType == 0)
            return false;

        foreach (var code in codes)
        {
            if ((code & 0x3) != 0 || ((code >> 14) & 0x3) != 0 || (code >> 16) != deviceType)
                return false;
        }

        return true;
    }

    private static int FindUnique(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        var first = haystack.IndexOf(needle);
        if (first < 0)
            return -1;

        var remaining = haystack[(first + 1)..];
        return remaining.IndexOf(needle) < 0 ? first : -1;
    }
}
