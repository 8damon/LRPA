using System.Runtime.InteropServices;
using System.Text.Json;
using Logitech.RawPeripheralAccess;
using Logitech.RawPeripheralAccess.ProtocolDiscovery;

try
{
    if (args.Length == 0)
        return Usage();

    switch (args[0].ToLowerInvariant())
    {
        case "protocol":
            {
                var protocol = ProtocolResolver.Resolve(args.Length > 1 ? args[1] : null);
                Write(new { ok = true, protocol });
                return 0;
            }
        case "inspect":
            {
                using var controller = new LrpaController();
                Write(new { ok = true, controller.DevicePath, controller.Protocol });
                return 0;
            }
        case "move" when args.Length == 3:
            {
                var x = int.Parse(args[1]);
                var y = int.Parse(args[2]);
                var created = LRPA.Initialize();
                var sent = LRPA.SendInput([LRPA.MouseInput(x, y, LRPA.MOUSEEVENTF_MOVE)]);
                Write(new { ok = true, sent, created, x, y });
                return 0;
            }
        case "self-test":
            {
                var protocol = ProtocolResolver.Resolve();
                var report = new MouseReport(MouseButtons.Button1 | MouseButtons.Button16, 80, -20, 1, -1);
                var encoded = Convert.ToHexString(report.Encode());
                var ok = INPUT.Size == 40 && encoded == "01805000ECFF01FF";
                Write(new { ok, inputSize = Marshal.SizeOf<INPUT>(), report = encoded, protocol });
                return ok ? 0 : 1;
            }
        default:
            return Usage();
    }
}
catch (Exception exception)
{
    Write(new { ok = false, error = exception.Message });
    return 1;
}
finally
{
    LRPA.Shutdown();
}

static int Usage()
{
    Console.Error.WriteLine("LRPA — Logitech Raw Peripheral Access");
    Console.Error.WriteLine("  lrpa protocol [driver-path]");
    Console.Error.WriteLine("  lrpa inspect");
    Console.Error.WriteLine("  lrpa move <x> <y>");
    Console.Error.WriteLine("  lrpa self-test");
    return 2;
}

static void Write(object value)
    => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
