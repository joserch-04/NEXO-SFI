using System.Buffers.Binary;
using System.IO.Compression;
namespace Nexo.Application;

// El navegador convierte el archivo elegido a PNG RGB/RGBA de 8 bits, sin entrelazado.
// La API comprueba independientemente estructura, dimensiones, CRC y datos comprimidos.
public static class BusinessIcon
{
    public const int MaxBytes = 128 * 1024;
    public const int MaxDimension = 256;
    private const string Prefix = "data:image/png;base64,";
    public static void Validate(string? dataUrl)
    {
        if (dataUrl is null) return;
        try
        {
            if (!dataUrl.StartsWith(Prefix, StringComparison.Ordinal) || dataUrl.Length > Prefix.Length + ((MaxBytes + 2) / 3 * 4)) throw new InvalidDataException();
            var bytes = Convert.FromBase64String(dataUrl[Prefix.Length..]);
            if (bytes.Length > MaxBytes || bytes.Length < 57 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException();
            var offset = 8; var width = 0; var height = 0; var channels = 0; var ended = false; var sawData = false;
            using var compressed = new MemoryStream();
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 12) throw new InvalidDataException();
                var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                if (lengthValue > MaxBytes) throw new InvalidDataException();
                var length = (int)lengthValue;
                if (length > bytes.Length - offset - 12) throw new InvalidDataException();
                var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                var body = bytes.AsSpan(offset + 8, length);
                var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + length, 4));
                if (Crc(bytes.AsSpan(offset + 4, length + 4)) != expectedCrc) throw new InvalidDataException();
                if (offset == 8 && type != "IHDR") throw new InvalidDataException();
                switch (type)
                {
                    case "IHDR":
                        if (width != 0 || offset != 8 || length != 13) throw new InvalidDataException();
                        var w = BinaryPrimitives.ReadUInt32BigEndian(body[..4]); var h = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(4, 4));
                        if (w is < 1 or > MaxDimension || h is < 1 or > MaxDimension || body[8] != 8 || body[9] is not (2 or 6) || body[10] != 0 || body[11] != 0 || body[12] != 0) throw new InvalidDataException();
                        width = (int)w; height = (int)h; channels = body[9] == 6 ? 4 : 3; break;
                    case "IDAT":
                        if (width == 0) throw new InvalidDataException();
                        sawData = true; compressed.Write(body); break;
                    case "IEND":
                        if (length != 0 || !sawData || offset + 12 != bytes.Length) throw new InvalidDataException();
                        ended = true; break;
                    default:
                        // Rechazar bloques críticos desconocidos (incluye una paleta no esperada).
                        if ((bytes[offset + 4] & 32) == 0) throw new InvalidDataException();
                        break;
                }
                offset += length + 12;
            }
            if (!ended || compressed.Length == 0) throw new InvalidDataException();
            compressed.Position = 0;
            using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[width * channels];
            for (var y = 0; y < height; y++)
            {
                var filter = inflater.ReadByte(); if (filter is < 0 or > 4) throw new InvalidDataException();
                inflater.ReadExactly(row);
            }
            if (inflater.ReadByte() != -1) throw new InvalidDataException();
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or EndOfStreamException)
        { throw new SecurityError(400, "invalid_business_icon"); }
    }
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffff;
        foreach (var value in data) { crc ^= value; for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0); }
        return ~crc;
    }
}
