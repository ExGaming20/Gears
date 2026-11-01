using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Gears.Utilities
{
    internal static class NativeLibraryPath
    {
        private const string BinFolderName = "bin";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Configure()
        {
            string binFolder = Path.Combine(AppContext.BaseDirectory, BinFolderName);
            if (!Directory.Exists(binFolder))
                return;

            // Preload native libraries before OpenTK/GLFW initializes.
            foreach (string library in Directory.EnumerateFiles(binFolder, "*.dll"))
            {
                try
                {
                    NativeLibrary.Load(library);
                }
                catch (DllNotFoundException)
                {
                    // Let OpenTK's normal resolution report the real error later.
                }
                catch (BadImageFormatException)
                {
                    // Ignore files that are not valid native libraries.
                }
            }

            // Fallback for other native libraries loaded later.
            SetDllDirectory(binFolder);
        }
    }
}
