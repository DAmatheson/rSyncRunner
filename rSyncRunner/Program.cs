using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace rSyncRunner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            SetupConsole();

            // If less than than minimum required number of args are supplied, display help and exit
            if (args.Length < Constants.MIN_NUMBER_OF_ARGS)
            {
                HelpMessage();

                return Constants.SUCCESS_CODE;
            }

            var settings = new Settings(args);

            Console.Title = settings.SyncFromPath + " - C# rSync Runner";

            // Verify folder isn't empty before beginning sync process
            if (!VerifyFolderStatus(settings.SyncFromPath))
            {
                Console.WriteLine("*** Warning: The folder is ~< 4 MB ***");
                Console.ReadKey();

                return Constants.ERROR_CODE;
            }

            // Begin rSync process
            int goodSync = LaunchRsync(
                settings.RSyncExePath, settings.RsyncFlags, settings.RsyncToPath,
                settings.RsyncFromPath);

            if (goodSync != Constants.RSYNC_SUCCESS_CODE)
            {
                Console.WriteLine("*** rSync exited abnormally ***");
                Console.ReadKey();

                return Constants.ERROR_CODE;
            }

            Console.WriteLine("--- Copy Complete ---");

            // Log cleanup
            if (settings.RsyncLogPath != null && settings.CleanLogPath != null)
            {
                Task.Delay(3000).Wait(); // Wait 3 seconds to allow rSync to finish with the log file

                bool goodCleanup = CleanupLogs(settings.RsyncLogPath, settings.CleanLogPath);

                if (!goodCleanup)
                {
                    Console.WriteLine("*** Log cleanup failed ***");
                    Console.ReadKey();

                    return Constants.ERROR_CODE;
                }

                Console.WriteLine("--- Log Cleanup Complete ---");
            }

            return Constants.SUCCESS_CODE;
        }

        // Required to get a handle for the console window
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        // Required to change the window state of the console window
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr windowHandle, int windowStateCode);

        private static void SetupConsole()
        {
            // Sets up the console window

            Console.Title = "C# rSync Runner";

            Console.WindowWidth = Console.LargestWindowWidth > 110 ? 110 : Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight > 30 ? 30 : Console.LargestWindowHeight;

            MinimizeWindow();
        }

        private static void MinimizeWindow()
        {
            // Gets the handle for the console window and minimizes it

            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, Constants.SW_MINIMIZED);
        }

        private static void RestoreWindow()
        {
            // Restores the window from minimized state

            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, Constants.SW_RESTORE);

            Console.WindowWidth = Console.LargestWindowWidth > 110 ? 110 : Console.LargestWindowWidth;
            Console.WindowHeight = Console.LargestWindowHeight > 30 ? 30 : Console.LargestWindowHeight;   
        }

        private static void HelpMessage()
        {
            // Displays a message explaining the arguments list

            RestoreWindow();
            Console.WindowHeight = Console.LargestWindowHeight > 40 ? 40 : Console.LargestWindowHeight;
            Console.BufferHeight = Console.BufferHeight < 70 ? 70 : Console.BufferHeight;

            string example =
                @"rSyncRunner.exe " + @"""D:\My Music"" " + 
                @"""C:\Program Files (x86)\cwRsync\bin\rsync.exe"" " +
                Environment.NewLine +  
                @"""-arv --progress --filter='. /cygdrive/d/My Documents/rsync/music-sync_filter.txt' " +
                @"--log-file='/cygdrive/d/My Documents/rsync/music-sync.log' --delete-before""" +
                Environment.NewLine +  
                @"""/cygdrive/d/My Music"" " + 
                @"""//DS212j/Disk 2/Music/Drew's Music/"" " +
                Environment.NewLine +  
                @"""D:\My Documents\rsync\music-sync.log"" " +
                @"""D:\My Documents\rsync\music-sync-clean.txt""";

            Console.WriteLine("---          Argument list:          ---");
            Console.WriteLine();
            Console.WriteLine(" - Required: -");
            Console.WriteLine("1 - Sync from path         -  To confirm the folder isn't empty");
            Console.WriteLine("2 - rSync Exe path         -  Full path including the .exe");
            Console.WriteLine("3 - rSync Flags            -  All '-' and '--' flags such as -a and --delete-before");
            Console.WriteLine("4 - rSync From Path        -  Path to sync files to (aka origin)");
            Console.WriteLine("5 - rSync To Path          -  Path to sync files from (aka destination)");
            Console.WriteLine();
            Console.WriteLine(" - Optional: -");
            Console.WriteLine("6 - rSync log file path    -  To read all deleted files and clear the log");
            Console.WriteLine("7 - Clean log file path    -  To write all the deleted files");
            Console.WriteLine();
            Console.WriteLine(
                "    Both 6 & 7 must both be used if included, in addition to the rsync --log-file flag");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("---      Example Usage (Spacing for clarity):      ---");
            Console.WriteLine();
            Console.WriteLine(example);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("The 'Sync from path' is used to check if the folder you are " +
                "syncing is ~> 4MB to prevent accidental deletion \nof destination files. The log " +
                "file paths are used to to find all of the items that were deleted, clear the \nrsync " +
                "log, and write the deleted items into the Clean log file for easier reading." +
                "\nThe application will close by itself if no errors occur while running. Otherwise, " +
                "it will stay open with an \nerror message until you press any key.");
            Console.WriteLine();
            Console.WriteLine("--- Press any key to exit ---");

            Console.ReadKey();
        }

        private static long DirectorySizeInKB(string path, bool recursive)
        {
            // Calculates the size of a directory. If recursive arg is true, recursively check directories

            long totalBytes;

            if (recursive)
            {
                totalBytes = Directory.EnumerateDirectories(path)
                    .SelectMany(directoryName => Directory.EnumerateFiles(directoryName, "*.*")).
                    Where(
                        fileName =>
                            !fileName.EndsWith(".ini") || fileName != "Thumbs.db").
                    Sum(directoryFileName => new FileInfo(directoryFileName).Length);
            }
            else
            {
                totalBytes = Directory.EnumerateFiles(path, "*.*").
                    Where(
                        fileName =>
                            !fileName.EndsWith(".ini") || fileName != "Thumbs.db").
                    Sum(fileName => new FileInfo(fileName).Length);
            }

            return totalBytes / Constants.BYTE_IN_KB;
        }

        private static bool VerifyFolderStatus(string path)
        {
            bool notEmpty = path != null && DirectorySizeInKB(path, false) > (Constants.KB_IN_MB * 4);

            // If after the first quick directory check the size is less than ~4MB, do a recursive check
            if (!notEmpty)
            {
                notEmpty = path != null && DirectorySizeInKB(path, true) > (Constants.KB_IN_MB * 4);
            }

            return notEmpty;
        }

        private static int LaunchRsync(string exePath, string flags, string to, string from)
        {
            int exitCode;

            var rSyncProcess = new ProcessStartInfo
            {
                Arguments = flags + " \"" + from + "\" \"" + to + "\"",
                FileName = exePath,
                WindowStyle = ProcessWindowStyle.Minimized,
                CreateNoWindow = false, // Don't start the process in a new window
                UseShellExecute = false // Don't use the OS shell to start the process
            };

            try
            {
                using (Process rSync = Process.Start(rSyncProcess))
                {
                    if (rSync != null)
                    {
                        rSync.WaitForExit();

                        exitCode = rSync.ExitCode;
                    }
                    else
                    {
                        exitCode = Constants.ERROR_CODE;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n*** The following error occured when launching rSync: ***");
                Console.WriteLine(ex.Message + "\n");

                throw;
            }

            return exitCode;
        }

        private static bool CleanupLogs(string rSyncLogPath, string cleanLogPath)
        {
            bool success = true;

            try
            {
                var keepLines = new List<string>();

                // Read in lines from rSyncs log file
                using (var rSyncReader = new StreamReader(rSyncLogPath))
                {
                    while (!rSyncReader.EndOfStream)
                    {
                        string line = rSyncReader.ReadLine();

                        if (line != null && line.ToLower().Contains("deleting"))
                        {
                            keepLines.Add(line);
                        }
                    }
                }

                // Write out lines into clean log file and empty rSync log file
                using (StreamWriter cleanWriter = new StreamWriter(cleanLogPath, true),
                                    rSyncWriter = new StreamWriter(rSyncLogPath))
                {
                    foreach (string line in keepLines)
                    {
                        cleanWriter.WriteLine(line); // Write all of the deleted items to the clean log
                    }

                    rSyncWriter.Write(""); // Write an empty string to clear the file
                }
            }
            catch (Exception ex) // Catch all exceptions but rethrow any that aren't IO related
            {
                success = false;

                if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Console.WriteLine("\n*** The following error occured: ***\n" + ex.Message + "\n");
                }
                else
                {
                    throw; // Rethrow the error because I can't handle it
                }
            }

            return success;
        }

        private struct Constants
        {
            // The minimum number of arguments required to run successfully
            public const int MIN_NUMBER_OF_ARGS = 5;

            // Data unit conversions
            public const int BYTE_IN_KB = 1024;
            public const int KB_IN_MB = BYTE_IN_KB;

            // Exit codes
            public const int RSYNC_SUCCESS_CODE = 0;
            public const int SUCCESS_CODE = 0;
            public const int ERROR_CODE = 1;

            // Window state codes for ShowWindow from user32.dll
            public const int SW_MINIMIZED = 2;
            public const int SW_RESTORE = 9;
        }
    }
}