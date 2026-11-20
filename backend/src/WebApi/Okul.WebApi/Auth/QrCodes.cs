using QRCoder;

namespace Okul.WebApi.Auth;

/// <summary>The 2FA setup QR code as a PNG data URI (plan 0004, R4): one image for web and mobile, no client library.</summary>
public static class QrCodes
{
    public static string PngDataUri(string content)
    {
        using var data = QRCodeGenerator.GenerateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 6);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
