namespace Logitech.RawPeripheralAccess.ProtocolDiscovery;

/// <summary>A validated private-protocol profile recovered from an installed driver image.</summary>
public sealed record DriverProtocol(
    string Fingerprint,
    string DriverPath,
    string FileVersion,
    string Sha256,
    long DispatchFileOffset,
    uint DispatchRva,
    uint MouseDescriptorRva,
    uint RemoveDeviceIoctl,
    uint SubmitMouseReportIoctl,
    uint CreateDeviceIoctl)
{
    public const int MouseDeviceType = 0x6D;
}
