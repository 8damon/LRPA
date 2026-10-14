using Logitech.RawPeripheralAccess;

try
{
    INPUT[] inputs =
    [
        new INPUT
        {
            type = LRPA.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 64,
                    dy = -16,
                    dwFlags = LRPA.MOUSEEVENTF_MOVE
                }
            }
        }
    ];

    var sent = LRPA.SendInput((uint)inputs.Length, inputs, INPUT.Size);
    Console.WriteLine($"Sent {sent} input record.");
}
finally
{
    LRPA.Shutdown();
}
