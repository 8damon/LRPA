using Logitech.RawPeripheralAccess.ProtocolDiscovery;

var protocol = ProtocolResolver.Resolve();

Console.WriteLine($"Fingerprint: {protocol.Fingerprint}");
Console.WriteLine($"Driver:      {protocol.DriverPath}");
Console.WriteLine($"Version:     {protocol.FileVersion}");
Console.WriteLine($"SHA-256:     {protocol.Sha256}");
Console.WriteLine($"Create:      0x{protocol.CreateDeviceIoctl:X8}");
Console.WriteLine($"Mouse:       0x{protocol.SubmitMouseReportIoctl:X8}");
Console.WriteLine($"Remove:      0x{protocol.RemoveDeviceIoctl:X8}");
