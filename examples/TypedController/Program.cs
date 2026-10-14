using Logitech.RawPeripheralAccess;

using var mouse = new LrpaController();
var created = mouse.TryCreateMouse();

Console.WriteLine(created ? "Created the virtual mouse." : "Attached to the existing virtual mouse.");
mouse.Move(40, 0);
mouse.Wheel(vertical: 1);
