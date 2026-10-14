using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Logitech.RawPeripheralAccess.ProtocolDiscovery.Fingerprints;

namespace Logitech.RawPeripheralAccess.ProtocolDiscovery;

/// <summary>Fast, cached and fail-closed recovery of the installed Logitech bus protocol.</summary>
public static class ProtocolResolver
{
    public const string DefaultDriverName = "logi_joy_bus_enum.x64.sys";

    private static readonly IProtocolFingerprint[] Fingerprints = [new BusSwitchStaircaseV1()];
    private static readonly ConcurrentDictionary<string, Lazy<DriverProtocol>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string GetDefaultDriverPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", DefaultDriverName);

    public static DriverProtocol Resolve(string? driverPath = null)
    {
        var path = Path.GetFullPath(driverPath ?? GetDefaultDriverPath());
        return Cache.GetOrAdd(path, static item =>
            new Lazy<DriverProtocol>(() => ResolveUncached(item),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static DriverProtocol ResolveUncached(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The Logitech G HUB virtual-bus driver was not found.", path);

        var image = File.ReadAllBytes(path);
        var pe = PeImage.Parse(image);
        var matched = false;
        FingerprintMatch result = default;
        string? fingerprint = null;

        foreach (var candidate in Fingerprints)
        {
            if (!candidate.TryMatch(image, pe, out var current))
                continue;
            if (matched)
                throw new InvalidDataException("Multiple protocol fingerprints matched; refusing an ambiguous profile.");
            matched = true;
            result = current;
            fingerprint = candidate.Id;
        }

        if (!matched)
            throw new InvalidDataException("No validated Logitech bus protocol fingerprint matched this driver.");

        return new DriverProtocol(
            fingerprint!,
            path,
            FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown",
            Convert.ToHexString(SHA256.HashData(image)),
            result.DispatchOffset,
            pe.OffsetToRva(result.DispatchOffset),
            pe.OffsetToRva(result.MouseDescriptorOffset),
            result.RemoveDeviceIoctl,
            result.SubmitMouseReportIoctl,
            result.CreateDeviceIoctl);
    }

}
