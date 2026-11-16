using QRCoder;
using QRCoder.Exceptions;

namespace Volterp.Api.Helpers;

// The QR of a document: the verification URL drawn as an image. The SVG is
// the one the printed representation carries, because it scales without losing
// lines; the PNG is the one a screen shows.
public static class QrCodeRenderer
{
    private const string DarkColor = "#000000";
    private const string LightColor = "#ffffff";

    // The PNG builder takes its colors as RGBA bytes, not as hex strings.
    private static readonly byte[] DarkColorRgba = { 0x00, 0x00, 0x00, 0xFF };
    private static readonly byte[] LightColorRgba = { 0xFF, 0xFF, 0xFF, 0xFF };

    // The DGII asks for the version 8 of the symbol in the printed
    // representation ("Descripción Técnica Servicios DGII", p. 41-42). The
    // error correction level (Q) is a different thing and does not decide the
    // version, so the version is asked for explicitly.
    public const int Version = 8;

    public static string Svg(string url, int pixelsPerModule = 10)
    {
        try
        {
            return SvgQRCodeHelper.GetQRCode(
                url,
                pixelsPerModule,
                DarkColor,
                LightColor,
                QRCodeGenerator.ECCLevel.Q,
                false,
                false,
                QRCodeGenerator.EciMode.Default,
                Version);
        }
        catch (DataTooLongException)
        {
            // The verification URL of the timbre does not fit in the version 8
            // at the level that suits a URL (108 bytes at Q, and the URL of the
            // spec is longer still). A symbol that encodes the URL is worth
            // more than one that carries the version the DGII named, so the
            // library sizes the symbol that does fit.
            return SvgQRCodeHelper.GetQRCode(
                url,
                pixelsPerModule,
                DarkColor,
                LightColor,
                QRCodeGenerator.ECCLevel.Q);
        }
    }

    public static byte[] Png(string url, int pixelsPerModule = 20)
    {
        try
        {
            return PngByteQRCodeHelper.GetQRCode(
                url,
                pixelsPerModule,
                DarkColorRgba,
                LightColorRgba,
                QRCodeGenerator.ECCLevel.Q,
                false,
                false,
                QRCodeGenerator.EciMode.Default,
                Version);
        }
        catch (DataTooLongException)
        {
            return PngByteQRCodeHelper.GetQRCode(
                url,
                pixelsPerModule,
                DarkColorRgba,
                LightColorRgba,
                QRCodeGenerator.ECCLevel.Q);
        }
    }
}
