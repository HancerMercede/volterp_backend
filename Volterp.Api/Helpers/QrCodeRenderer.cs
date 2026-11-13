using QRCoder;

namespace Volterp.Api.Helpers;

// The QR of a document: the verification URL drawn as an image. The SVG is
// the one the printed representation carries, because it scales without losing
// lines; the PNG is the one a screen shows.
public static class QrCodeRenderer
{
    private const string DarkColor = "#000000";
    private const string LightColor = "#ffffff";

    // The DGII asks for the version 8 of the QR code ("Descripción Técnica
    // Servicios DGII", p. 42), which is what the correction level Q gives.
    public static string Svg(string url, int pixelsPerModule = 10)
        => SvgQRCodeHelper.GetQRCode(url, pixelsPerModule, DarkColor, LightColor, QRCodeGenerator.ECCLevel.Q);

    public static byte[] Png(string url, int pixelsPerModule = 20)
        => PngByteQRCodeHelper.GetQRCode(url, QRCodeGenerator.ECCLevel.Q, pixelsPerModule);
}
