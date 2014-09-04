using System;
using System.Collections.Generic;

namespace rSyncRunner
{
    public class Settings
    {
        // Initializes properties with the matching arg or null if it isn't found
        public Settings(IList<string> args)
        {
            SyncFromPath = args.Count >= 1 ? args[0] : null;
            RSyncExePath = args.Count >= 2 ? args[1] : null;
            RsyncFlags = args.Count >= 3 ? args[2] : null;
            RsyncFromPath = args.Count >= 4 ? args[3] : null;
            RsyncToPath = args.Count >= 5 ? args[4] : null;
            RsyncLogPath = args.Count >= 6 ? args[5] : null;
            CleanLogPath = args.Count >= 7 ? args[6] : null;
        }

        public string SyncFromPath { get; private set; }
        public string RSyncExePath { get; private set; }
        public string RsyncFlags { get; private set; }
        public string RsyncFromPath { get; private set; }
        public string RsyncToPath { get; private set; }
        public string RsyncLogPath { get; private set; }
        public string CleanLogPath { get; private set; }

        public void PrintArgs()
        {
            Console.WriteLine("SyncFromPath: " + SyncFromPath);
            Console.WriteLine("RSyncExePath: " + RSyncExePath);
            Console.WriteLine("RsyncFlags: " + RsyncFlags);
            Console.WriteLine("RsyncFromPath: " + RsyncFromPath);
            Console.WriteLine("RsyncToPath: " + RsyncToPath);
            Console.WriteLine("RsyncLogPath: " + RsyncLogPath);
            Console.WriteLine("CleanLogPath: " + CleanLogPath);
        }
    }
}