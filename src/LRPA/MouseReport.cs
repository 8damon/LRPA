using System.Buffers.Binary;

namespace Logitech.RawPeripheralAccess;

[Flags]
public enum MouseButtons : ushort
{
    None = 0,
    Button1 = 1 << 0,
    Button2 = 1 << 1,
    Button3 = 1 << 2,
    Button4 = 1 << 3,
    Button5 = 1 << 4,
    Button6 = 1 << 5,
    Button7 = 1 << 6,
    Button8 = 1 << 7,
    Button9 = 1 << 8,
    Button10 = 1 << 9,
    Button11 = 1 << 10,
    Button12 = 1 << 11,
    Button13 = 1 << 12,
    Button14 = 1 << 13,
    Button15 = 1 << 14,
    Button16 = 1 << 15
}

public readonly record struct MouseReport(
    MouseButtons Buttons,
    short X,
    short Y,
    sbyte Wheel = 0,
    sbyte HorizontalPan = 0)
{
    public const int ByteLength = 8;

    public void Write(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException($"Destination must contain at least {ByteLength} bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)Buttons);
        BinaryPrimitives.WriteInt16LittleEndian(destination[2..], X);
        BinaryPrimitives.WriteInt16LittleEndian(destination[4..], Y);
        destination[6] = unchecked((byte)Wheel);
        destination[7] = unchecked((byte)HorizontalPan);
    }

    public byte[] Encode()
    {
        var bytes = new byte[ByteLength];
        Write(bytes);
        return bytes;
    }
}
