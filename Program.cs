using Gears;
using Gears.Utilities;

NativeLibraryPath.Configure();

using (Game game = new Game(1280, 720, "Gears"))
{
    game.Run();
}