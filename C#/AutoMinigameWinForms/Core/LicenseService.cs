using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMinigameWinForms.Models;

namespace AutoMinigameWinForms.Core;

public sealed class LicenseService : IDisposable
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public LicenseService()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(AppConstants.LicenseApiTimeoutSec),
        };
    }

    public bool EnsureLicenseOrExit(string configPath, AppConfig cfg, IWin32Window? owner = null)
    {
        // Force fresh login on every app launch.
        // Saved key is still pre-filled in the dialog, but startup never auto-validates.
        cfg.LicenseLastMessage = "Silakan login dan validasi license untuk sesi ini.";
        TrySave(configPath, cfg);

        using var dlg = new LicenseForm(cfg, this);
        if (dlg.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        cfg.Software = AppConstants.SoftwareName;
        TrySave(configPath, cfg);
        return true;
    }

    public (bool ok, string message) RevalidateStoredLicense(string configPath, AppConfig cfg)
    {
        return RevalidateStoredLicenseAsync(configPath, cfg).GetAwaiter().GetResult();
    }

    public async Task<(bool ok, string message)> RevalidateStoredLicenseAsync(string configPath, AppConfig cfg, CancellationToken cancellationToken = default)
    {
        var savedKey = (cfg.LicenseKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(savedKey))
        {
            cfg.LicenseLastMessage = "License key belum diisi.";
            TrySave(configPath, cfg);
            return (false, cfg.LicenseLastMessage);
        }

        var check = await ValidateAsync(savedKey, cancellationToken);
        if (!check.ok)
        {
            cfg.LicenseLastMessage = check.message;
            TrySave(configPath, cfg);
            return (false, check.message);
        }

        ApplyValidationResult(cfg, savedKey, check.response, check.machineId, check.message);
        TrySave(configPath, cfg);
        return (true, check.message);
    }

    public string GetMachineId()
    {
        return GetMachineIdCandidates()[0];
    }

    public async Task<(bool ok, string message, LicenseValidationResponse? response, LicenseValidationFailureKind failureKind, string machineId)> ValidateAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var key = (licenseKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (false, "License key wajib diisi.", null, LicenseValidationFailureKind.Input, GetMachineId());
        }

        var candidates = GetMachineIdCandidates();
        (bool ok, string message, LicenseValidationResponse? response, LicenseValidationFailureKind failureKind, string machineId) lastResult =
            (false, "License tidak valid.", null, LicenseValidationFailureKind.InvalidLicense, candidates[0]);

        foreach (var machineId in candidates)
        {
            var result = await ValidateWithMachineAsync(key, machineId, cancellationToken);
            if (result.ok)
            {
                return result;
            }

            lastResult = result;
            if (result.failureKind is LicenseValidationFailureKind.Input
                or LicenseValidationFailureKind.Network
                or LicenseValidationFailureKind.Timeout
                or LicenseValidationFailureKind.Server)
            {
                return result;
            }
        }

        return lastResult;
    }

    public void ApplyValidationResult(AppConfig cfg, string licenseKey, LicenseValidationResponse? response, string machineId, string message)
    {
        cfg.LicenseKey = (licenseKey ?? string.Empty).Trim().ToUpperInvariant();
        cfg.Software = AppConstants.SoftwareName;
        cfg.MachineId = machineId;
        cfg.LicenseOwner = response?.LicenseOwner?.Trim() ?? string.Empty;
        cfg.LicenseExpiresAt = response?.ExpiresAt?.Trim() ?? string.Empty;
        cfg.LicenseLastVerifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        cfg.LicenseLastMessage = message ?? string.Empty;
    }

    private async Task<(bool ok, string message, LicenseValidationResponse? response, LicenseValidationFailureKind failureKind, string machineId)> ValidateWithMachineAsync(
        string licenseKey,
        string machineId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = BuildRequest(licenseKey, machineId);
            using var response = await _client.PostAsJsonAsync(GetApiUrl(), request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var apiMessage = ExtractApiMessage(content);
                return (
                    false,
                    BuildFriendlyErrorMessage(response.StatusCode, apiMessage),
                    null,
                    MapFailureKind(response.StatusCode),
                    machineId
                );
            }

            var result = JsonSerializer.Deserialize<LicenseValidationResponse>(content, JsonOptions);
            if (result is null)
            {
                return (false, "Respons server aktivasi tidak valid.", null, LicenseValidationFailureKind.Server, machineId);
            }

            var message = string.IsNullOrWhiteSpace(result.Message)
                ? (result.Valid ? "License valid." : "License tidak valid.")
                : result.Message;

            return (
                result.Valid,
                message,
                result,
                result.Valid ? LicenseValidationFailureKind.None : LicenseValidationFailureKind.InvalidLicense,
                machineId
            );
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (false, "Verifikasi license dibatalkan.", null, LicenseValidationFailureKind.Timeout, machineId);
        }
        catch (TaskCanceledException)
        {
            return (false, "Server aktivasi terlalu lama merespons. Coba lagi sebentar lagi.", null, LicenseValidationFailureKind.Timeout, machineId);
        }
        catch (HttpRequestException)
        {
            return (false, "Tidak bisa terhubung ke server aktivasi. Cek koneksi atau URL server license.", null, LicenseValidationFailureKind.Network, machineId);
        }
        catch (Exception ex)
        {
            return (false, $"Terjadi kesalahan saat memverifikasi license. {ex.Message}", null, LicenseValidationFailureKind.Server, machineId);
        }
    }

    private static LicenseValidationRequest BuildRequest(string licenseKey, string machineId)
    {
        return new LicenseValidationRequest
        {
            LicenseKey = licenseKey,
            ProductCode = AppConstants.LicenseProductCode,
            MachineId = machineId,
            MachineName = GetMachineName(),
            UserName = GetUserName(),
        };
    }

    private static IReadOnlyList<string> GetMachineIdCandidates()
    {
        var machineName = GetMachineName();
        var userName = GetUserName();

        var candidates = new List<string>
        {
            BuildMachineId($"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion.VersionString}"),
            BuildMachineId($"{machineName}|{userName}|{GetPythonStyleNodeDecimal()}"),
            BuildMachineId($"{machineName}|{userName}|{GetLegacyHardwareNodeHex()}"),
        };

        return candidates
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildMachineId(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static string GetApiUrl()
    {
        var fromEnv = Environment.GetEnvironmentVariable("LICENSE_API_URL");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        return AppConstants.LicenseApiUrl;
    }

    private static string ExtractApiMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            var result = JsonSerializer.Deserialize<LicenseValidationResponse>(content, JsonOptions);
            return result?.Message?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildFriendlyErrorMessage(HttpStatusCode statusCode, string apiMessage)
    {
        if (!string.IsNullOrWhiteSpace(apiMessage))
        {
            return statusCode switch
            {
                HttpStatusCode.NotFound => $"{apiMessage} Periksa kembali license key kamu.",
                HttpStatusCode.Forbidden => apiMessage,
                HttpStatusCode.BadRequest => apiMessage,
                _ => apiMessage,
            };
        }

        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Data aktivasi belum lengkap. Periksa license key lalu coba lagi.",
            HttpStatusCode.Unauthorized => "Permintaan aktivasi ditolak oleh server.",
            HttpStatusCode.Forbidden => "License ditolak oleh server aktivasi.",
            HttpStatusCode.NotFound => "License tidak ditemukan. Periksa kembali license key kamu.",
            HttpStatusCode.TooManyRequests => "Terlalu banyak percobaan aktivasi. Coba lagi beberapa saat.",
            HttpStatusCode.RequestTimeout => "Server aktivasi terlalu lama merespons. Coba lagi sebentar lagi.",
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => "Server aktivasi sedang bermasalah. Coba lagi nanti.",
            _ => $"Verifikasi license gagal. Kode server: {(int)statusCode}.",
        };
    }

    private static LicenseValidationFailureKind MapFailureKind(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => LicenseValidationFailureKind.Input,
            HttpStatusCode.NotFound or HttpStatusCode.Forbidden => LicenseValidationFailureKind.InvalidLicense,
            HttpStatusCode.RequestTimeout => LicenseValidationFailureKind.Timeout,
            HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => LicenseValidationFailureKind.Server,
            _ => LicenseValidationFailureKind.Server,
        };
    }

    private static string GetMachineName()
    {
        return Environment.GetEnvironmentVariable("COMPUTERNAME")
            ?? Environment.MachineName;
    }

    private static string GetUserName()
    {
        return Environment.GetEnvironmentVariable("USERNAME")
            ?? Environment.UserName;
    }

    private static string GetPythonStyleNodeDecimal()
    {
        try
        {
            var selectedHex = GetMacHexCandidates()
                .OrderBy(x => IsLocallyAdministered(x) ? 1 : 0)
                .ThenBy(x => x, StringComparer.Ordinal)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(selectedHex))
            {
                return "0";
            }

            if (ulong.TryParse(selectedHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Fallback below.
        }

        return "0";
    }

    private static string GetLegacyHardwareNodeHex()
    {
        try
        {
            var adapters = GetMacHexCandidates()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            return adapters.Length > 0 ? adapters[0] : "0";
        }
        catch
        {
            return "0";
        }
    }

    private static IEnumerable<string> GetMacHexCandidates()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n =>
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(n => n.GetPhysicalAddress()?.ToString()?.Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x) && x!.Length >= 12)
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal);
    }

    private static bool IsLocallyAdministered(string macHex)
    {
        if (string.IsNullOrWhiteSpace(macHex) || macHex.Length < 2)
        {
            return true;
        }

        if (!byte.TryParse(macHex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var firstByte))
        {
            return true;
        }

        return (firstByte & 0x02) != 0;
    }

    private static void TrySave(string path, AppConfig cfg)
    {
        try
        {
            ConfigStore.Save(path, cfg);
        }
        catch
        {
            // Ignore config write errors; license flow should continue.
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
