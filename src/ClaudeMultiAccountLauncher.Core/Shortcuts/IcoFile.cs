using System.Buffers.Binary;

namespace ClaudeMultiAccountLauncher.Core.Shortcuts;

/// <summary>
/// Builds a .ico file from an exe's RT_GROUP_ICON directory and its RT_ICON images.
/// The group entry layout matches the .ico entry except the last field: a 2-byte resource
/// id instead of a 4-byte file offset.
/// </summary>
public static class IcoFile
{
    private const int HeaderSize = 6;
    private const int GroupEntrySize = 14;
    private const int FileEntrySize = 16;

    /// <param name="groupDirectory">Raw RT_GROUP_ICON resource.</param>
    /// <param name="loadImage">Returns the RT_ICON resource for a resource id.</param>
    public static byte[] FromResourceGroup(ReadOnlySpan<byte> groupDirectory, Func<ushort, byte[]> loadImage)
    {
        if (groupDirectory.Length < HeaderSize || BinaryPrimitives.ReadUInt16LittleEndian(groupDirectory[2..]) != 1)
        {
            throw new InvalidDataException("Not an icon group resource.");
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(groupDirectory[4..]);
        if (count == 0 || groupDirectory.Length < HeaderSize + count * GroupEntrySize)
        {
            throw new InvalidDataException("Icon group resource is truncated.");
        }

        var images = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(groupDirectory[(HeaderSize + i * GroupEntrySize + 12)..]);
            images[i] = loadImage(id);
        }

        var output = new byte[HeaderSize + count * FileEntrySize + images.Sum(img => img.Length)];
        var span = output.AsSpan();
        groupDirectory[..HeaderSize].CopyTo(span); // reserved, type=1, count

        var offset = HeaderSize + count * FileEntrySize;
        for (var i = 0; i < count; i++)
        {
            var entry = span[(HeaderSize + i * FileEntrySize)..];
            groupDirectory.Slice(HeaderSize + i * GroupEntrySize, 8).CopyTo(entry); // w, h, colors, reserved, planes, bpp
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)images[i].Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], (uint)offset);
            images[i].CopyTo(span[offset..]);
            offset += images[i].Length;
        }

        return output;
    }
}
