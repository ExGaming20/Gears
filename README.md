# Gears Engine

Last updated: November 19, 2026

## Important Info

The engine is still being worked on, so this README won't be updated every commit. I'll update it when there's a big change or if something breaks after a change.

Also I have used the assistance of AI to write long scripts that I know the functionality of and to add simple script overviews. I do verify and moderate the code it gives and I dont just paste it into the project. I make sure that the code is correct and maintainable.

## Status

Still in development. Not ready to be used in a game yet.

## Overview

This is an engine called **Gears** that I'm making mostly for personal use. My goal is to eventually make an engine that can support a good-looking multiplayer game, but that's a long way off.

The project is for .NET 8 on Windows using NativeAOT. The engine and game share the same repo, SDK, and build.

The engine uses OpenTK and StbImageSharp. The shader code uses 4.3 Core.

The repo is organized into folders like `Graphics`, `World`, `Input`, `Utilities`, and `Diagnostics`. There's also an asset bundle tool and a `gears-bundle-maker.html` file in the repo.

As of November 2026, it has:

- Forward+ rendering with PBR
- Shader hot reloading
- Asset bundle system
- GameObjects with components
- Scene loading
- Simple input system
- Simple optimizations
- Some other stuff

## How to Build

To build the game/engine, run:

```bash
dotnet publish -c Release
```

The build will be in:

```text
bin\Release\net8.0-windows\win-x64\publish
```

If you just want to do a quick test build, run:

```bash
dotnet build
```

## How to Use the Build

The build folder looks like this:

```text
Root/
├── Assets/
├── Fallback Assets/
├── bin/
└── {Name}.exe
```

The `bin` folder has the DLLs. The `Fallback Assets` folder has fallback stuff for missing textures, models, shaders/raw shaders, and materials.

## License

MIT