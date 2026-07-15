using System;
using System.IO;
using System.Text.Json;

namespace MalodyVThemeLoader.Shared;

public static class ConsentReceipt
{
    public const string RelativePath = "ThemeLoader/authorization/consent.json";

    public static void Write(string gameDirectory, string context)
    {
        var authorization = DeviceAuthorization.CheckCurrentDevice();
        if (!authorization.Authorized)
        {
            throw new UnauthorizedAccessException(authorization.Error);
        }

        var receipt = new ConsentReceiptDocument
        {
            TermsVersion = LegalTerms.Version,
            DeviceCode = authorization.DeviceCode,
            Context = context,
            AcceptedAt = DateTimeOffset.UtcNow
        };
        receipt.Signature = Sign(receipt);

        var path = PathRules.GetSafeCombinedPath(gameDirectory, RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".new";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(receipt, JsonDefaults.Options));
        File.Move(temporary, path, true);
    }

    public static bool IsValid(string gameDirectory, out string error)
    {
        try
        {
            var path = PathRules.GetSafeCombinedPath(gameDirectory, RelativePath);
            if (!File.Exists(path))
            {
                error = "未找到有效的三遍条款阅读凭据。请先运行桌面主题管理器。";
                return false;
            }

            var receipt = JsonSerializer.Deserialize<ConsentReceiptDocument>(
                File.ReadAllText(path), JsonDefaults.Options);
            var authorization = DeviceAuthorization.CheckCurrentDevice();
            if (receipt == null
                || !authorization.Authorized
                || !string.Equals(
                    receipt.TermsVersion,
                    LegalTerms.Version,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.DeviceCode,
                    authorization.DeviceCode,
                    StringComparison.OrdinalIgnoreCase)
                || !DeviceAuthorization.VerifySignature(
                    "mvtl-consent-v1",
                    GetSignatureValue(receipt),
                    receipt.Signature))
            {
                error = "条款凭据无效或不属于当前设备。";
                return false;
            }

            error = "";
            return true;
        }
        catch (Exception exception)
        {
            error = $"条款凭据校验失败：{exception.Message}";
            return false;
        }
    }

    private static string Sign(ConsentReceiptDocument receipt)
    {
        return DeviceAuthorization.Sign(
            "mvtl-consent-v1",
            GetSignatureValue(receipt));
    }

    private static string GetSignatureValue(ConsentReceiptDocument receipt)
    {
        return string.Join("|",
            receipt.TermsVersion,
            receipt.DeviceCode,
            receipt.Context,
            receipt.AcceptedAt.ToUnixTimeSeconds());
    }
}

public sealed class ConsentReceiptDocument
{
    public int FormatVersion { get; set; } = 1;
    public string TermsVersion { get; set; } = "";
    public string DeviceCode { get; set; } = "";
    public string Context { get; set; } = "";
    public DateTimeOffset AcceptedAt { get; set; }
    public string Signature { get; set; } = "";
}
