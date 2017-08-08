﻿using System;
using NuKeeper.Configuration;

namespace NuKeeper
{
    public class Program
    {
        public static int Main(string[] args)
        {
            TempFiles.DeleteExistingTempDirs();
                
            var settings = SettingsParser.ReadSettings(args);

            if(settings == null)
            {
                Console.WriteLine("Exiting early...");
                return 1;
            }

            var container = ContainerRegistration.Init(settings);

            // get some storage space
            var tempDir = TempFiles.MakeUniqueTemporaryPath();

            var engine = container.GetInstance<GithubEngine>();
            engine.Run(tempDir)
                .GetAwaiter().GetResult();

            return 0;
        }
    }
}
