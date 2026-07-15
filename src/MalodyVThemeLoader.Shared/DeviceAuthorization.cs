using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace MalodyVThemeLoader.Shared;

public static class DeviceAuthorization
{
    private const string ResourceName =
        "MalodyVThemeLoader.Shared.allowed-devices.json";
    private const int Pbkdf2Iterations = 120_000;

    public static DeviceAuthorizationResult CheckCurrentDevice()
    {
        try
        {
            var deviceCode = GetDeviceCode();
            var authorizationValue = CreateAuthorizationValue(deviceCode);
            var allowList = ReadAllowList();

            foreach (var entry in allowList.Devices.Where(entry => entry.Enabled))
            {
                if (VerifyEntry(authorizationValue, entry))
                {
                    return new DeviceAuthorizationResult(
                        true, deviceCode, entry.Label, "");
                }
            }

            return new DeviceAuthorizationResult(
                false,
                deviceCode,
                "",
                "此设备未列入 ThemeLoader 的授权设备清单。");
        }
        catch (Exception exception)
        {
            return new DeviceAuthorizationResult(
                false,
                "unavailable",
                "",
                $"设备授权校验失败：{exception.Message}");
        }
    }

    public static string GetDeviceCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "设备授权仅支持 Windows。");
        }

        var machineGuid = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
            "MachineGuid",
            null) as string;
        if (string.IsNullOrWhiteSpace(machineGuid))
        {
            throw new InvalidOperationException("无法读取 Windows MachineGuid。");
        }

        var material = string.Join("|",
            "MalodyVThemeLoader",
            machineGuid.Trim().ToUpperInvariant(),
            Environment.Is64BitOperatingSystem ? "x64" : "x86");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
    }

    public static string CreateAuthorizationValue(string deviceCode)
    {
        using var hmac = new HMACSHA256(GetApplicationKey());
        var payload = Encoding.UTF8.GetBytes(
            $"mvtl-device-v1|{deviceCode.Trim().ToLowerInvariant()}");
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    public static AuthorizedDevice CreateAllowListEntry(
        string label,
        string authorizationValue)
    {
        var salt = RandomNumberGenerator.GetBytes(24);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(authorizationValue.Trim().ToLowerInvariant()),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            32);
        return new AuthorizedDevice
        {
            Id = Guid.NewGuid().ToString("N"),
            Label = label,
            Salt = Convert.ToBase64String(salt),
            AuthorizationHash = Convert.ToBase64String(hash),
            Enabled = true
        };
    }

    public static string Sign(string purpose, string value)
    {
        using var hmac = new HMACSHA256(GetApplicationKey());
        var bytes = Encoding.UTF8.GetBytes($"{purpose}|{value}");
        return Convert.ToHexString(hmac.ComputeHash(bytes)).ToLowerInvariant();
    }

    public static bool VerifySignature(string purpose, string value, string signature)
    {
        try
        {
            var expected = Convert.FromHexString(Sign(purpose, value));
            var actual = Convert.FromHexString(signature);
            return expected.Length == actual.Length
                   && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    private static DeviceAllowList ReadAllowList()
    {
        using var stream = typeof(DeviceAuthorization).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("授权设备清单未嵌入程序。");
        return JsonSerializer.Deserialize<DeviceAllowList>(stream, JsonDefaults.Options)
               ?? throw new InvalidDataException("授权设备清单为空。");
    }

    private static bool VerifyEntry(
        string authorizationValue,
        AuthorizedDevice entry)
    {
        try
        {
            var salt = Convert.FromBase64String(entry.Salt);
            var expected = Convert.FromBase64String(entry.AuthorizationHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(
                    authorizationValue.Trim().ToLowerInvariant()),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GetApplicationKey()
    {
        var first = new byte[]
        {
            0x51, 0x32, 0xA8, 0x17, 0xC4, 0x6D, 0x09, 0xE1,
            0x7A, 0xB5, 0x20, 0xCC, 0x93, 0x48, 0xF2, 0x3B
        };
        var second = new byte[]
        {
            0x24, 0x77, 0xD9, 0x62, 0xA1, 0x08, 0x5F, 0xB4,
            0x0D, 0xE0, 0x71, 0x99, 0x36, 0x1C, 0x87, 0x4E
        };
        var key = new byte[first.Length];
        for (var index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(first[index] ^ second[key.Length - index - 1] ^ 0x5A);
        }

        return SHA256.HashData(key);
    }
}

public sealed class DeviceAuthorizationResult
{
    public DeviceAuthorizationResult(
        bool authorized,
        string deviceCode,
        string label,
        string error)
    {
        Authorized = authorized;
        DeviceCode = deviceCode;
        Label = label;
        Error = error;
    }

    public bool Authorized { get; }
    public string DeviceCode { get; }
    public string Label { get; }
    public string Error { get; }
}

public sealed class DeviceAllowList
{
    public int FormatVersion { get; set; } = 1;
    public List<AuthorizedDevice> Devices { get; set; } = new();
}

public sealed class AuthorizedDevice
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Salt { get; set; } = "";
    public string AuthorizationHash { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
