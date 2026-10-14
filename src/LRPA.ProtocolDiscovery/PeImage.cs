using System.Buffers.Binary;

namespace Logitech.RawPeripheralAccess.ProtocolDiscovery;

internal sealed class PeImage
{
    private readonly Section[] _sections;

    private PeImage(Section[] sections) => _sections = sections;

    public ReadOnlySpan<Section> Sections => _sections;

    public uint OffsetToRva(int offset)
    {
        foreach (var section in _sections)
        {
            if (offset >= section.RawOffset && offset < section.RawOffset + section.RawSize)
                return checked(section.VirtualAddress + (uint)(offset - section.RawOffset));
        }

        throw new InvalidDataException($"File offset 0x{offset:X} is outside PE sections.");
    }

    public static PeImage Parse(ReadOnlySpan<byte> image)
    {
        if (image.Length < 0x40 || image[0] != 'M' || image[1] != 'Z')
            throw new InvalidDataException("Driver is not a valid DOS/PE image.");

        var peOffset = ReadInt32(image, 0x3C);
        if (peOffset < 0 || peOffset > image.Length - 24 || ReadUInt32(image, peOffset) != 0x00004550)
            throw new InvalidDataException("Driver has an invalid PE header.");

        var count = ReadUInt16(image, peOffset + 6);
        var optionalSize = ReadUInt16(image, peOffset + 20);
        var table = checked(peOffset + 24 + optionalSize);
        if (count == 0 || table > image.Length - count * 40)
            throw new InvalidDataException("Driver has an invalid PE section table.");

        var sections = new Section[count];
        for (var index = 0; index < count; index++)
        {
            var entry = table + index * 40;
            var rawSize = checked((int)ReadUInt32(image, entry + 16));
            var rawOffset = checked((int)ReadUInt32(image, entry + 20));
            if (rawOffset < 0 || rawSize < 0 || rawOffset > image.Length - rawSize)
                throw new InvalidDataException("Driver has an out-of-range PE section.");

            sections[index] = new Section(
                ReadUInt32(image, entry + 12),
                rawOffset,
                rawSize,
                (ReadUInt32(image, entry + 36) & 0x20000000) != 0);
        }

        return new PeImage(sections);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> image, int offset)
        => BinaryPrimitives.ReadUInt16LittleEndian(image.Slice(offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> image, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(image.Slice(offset, sizeof(uint)));

    private static int ReadInt32(ReadOnlySpan<byte> image, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(image.Slice(offset, sizeof(int)));

    internal readonly record struct Section(
        uint VirtualAddress,
        int RawOffset,
        int RawSize,
        bool IsExecutable);
}
