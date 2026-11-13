using System.Text;

namespace Aetherworks.Features;

// Called only on a SeString text payload. Icons and formatting payloads are opaque.
internal static class PartyLevelText
{
    public static byte[] RemoveLevel(ReadOnlySpan<byte> row, int start, int end)
    {
        if (end > start && end < row.Length && row[end] == (byte)' ') end++;
        return [..row[..start], ..row[end..], 0];
    }

    public static (int Start, int End) Locate(ReadOnlySpan<byte> text)
    {
        for (int offset = 0; offset < text.Length;)
        {
            if (Rune.DecodeFromUtf8(text[offset..], out var rune, out int length) != System.Buffers.OperationStatus.Done)
                return (-1, -1);
            int value = rune.Value;
            if (value is >= 0xE060 and <= 0xE06C)
            {
                int end = offset + length;
                while (end < text.Length && Rune.DecodeFromUtf8(text[end..], out rune, out length)
                       == System.Buffers.OperationStatus.Done && rune.Value is >= 0xE060 and <= 0xE069)
                    end += length;
                return (offset, end);
            }
            // If the HUD omits the level, insert directly before the name.
            if (Rune.IsLetterOrDigit(rune)) return (offset, offset);
            offset += length;
        }
        return (-1, -1);
    }
}
