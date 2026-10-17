using System.Buffers.Binary;
using System.IO.Compression;
using Nexo.Application;
using Xunit;
namespace Nexo.UnitTests;
public class BusinessIconTests
{
    // Fixture PNG RGBA generado con el formato estándar; no requiere paquetes de imagen.
    public static string Png(uint width = 1, uint height = 1)
    {
        using var png = new MemoryStream(); png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] body)
        {
            var data = System.Text.Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
            Span<byte> value = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(value, (uint)body.Length); png.Write(value); png.Write(data);
            uint crc = 0xffffffff; foreach (var b in data) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0); }
            BinaryPrimitives.WriteUInt32BigEndian(value, ~crc); png.Write(value);
        }
        var header = new byte[13]; BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), width); BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), height); header[8] = 8; header[9] = 6;
        Chunk("IHDR", header);
        using var data = new MemoryStream();
        using (var zlib = new ZLibStream(data, CompressionMode.Compress, true)) zlib.Write(new byte[] { 0, 20, 130, 80, 255 });
        Chunk("IDAT", data.ToArray()); Chunk("IEND", []);
        return "data:image/png;base64," + Convert.ToBase64String(png.ToArray());
    }
    [Fact]
    public void Accepts_small_png_and_removing_image() { BusinessIcon.Validate(Png()); BusinessIcon.Validate(null); }
    [Theory]
    [InlineData(0, 1)] [InlineData(257, 1)] [InlineData(1, 257)]
    public void Rejects_dimensions(uint width, uint height) => Assert.Equal("invalid_business_icon", Assert.Throws<SecurityError>(() => BusinessIcon.Validate(Png(width, height))).Code);
    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/icon.png")]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("data:image/png;base64,invalid")]
    public void Rejects_unknown_formats_and_invalid_content(string value) => Assert.Throws<SecurityError>(() => BusinessIcon.Validate(value));
    [Fact]
    public void Rejects_corrupted_truncated_and_over_limit_images()
    {
        var data = Convert.FromBase64String(Png().Split(',')[1]); data[20] ^= 1;
        Assert.Throws<SecurityError>(() => BusinessIcon.Validate("data:image/png;base64," + Convert.ToBase64String(data)));
        Assert.Throws<SecurityError>(() => BusinessIcon.Validate(Png()[..^8]));
        Assert.Throws<SecurityError>(() => BusinessIcon.Validate("data:image/png;base64," + new string('A', 180000)));
        Assert.Throws<SecurityError>(() => BusinessIcon.Validate(Png(2, 2))); // Cabecera válida pero datos insuficientes.
    }
}
