namespace Logitech.RawPeripheralAccess.ProtocolDiscovery.Fingerprints;

internal interface IProtocolFingerprint
{
    string Id { get; }
    bool TryMatch(ReadOnlySpan<byte> image, PeImage pe, out FingerprintMatch match);
}

internal readonly record struct FingerprintMatch(
    int DispatchOffset,
    int MouseDescriptorOffset,
    uint RemoveDeviceIoctl,
    uint SubmitMouseReportIoctl,
    uint CreateDeviceIoctl);
