using MBBSEmu.Btrieve;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.IO;
using System;

namespace MBBSUtil
{
    /// <summary>
    ///   An MBBSEmu database (.DB) utility program.
    ///
    ///   </para/>Currently supports two modes of operation, view and convert.
    ///   View mode shows information about the specified DAT file, such as key information.
    ///   Convert mode converts the DAT file into a .DB file.
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            new Program().Run(args);
        }

        private ILogger CreateLogger()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("MBBSEmu.Util", LogLevel.Debug)
                    .AddConsole();
            });
            return loggerFactory.CreateLogger("MBBSEmu.Util");
        }

        private void Run(string[] args)
        {
            var logger = CreateLogger();

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: MBBSDatabase [view|convert] [files]");
                return;
            }

            var convert = (args[0] == "convert");

            foreach (string s in args.Skip(1))
            {
                BtrieveFile file = new BtrieveFile();
                try
                {
                    logger.LogInformation($"Attempting to open {s}");

                    file.LoadFile(logger, s);
                    if (convert)
                    {
                        using var processor = new BtrieveFileProcessor();

                        var dbPath = Path.ChangeExtension(s, ".DB");
                        if (File.Exists(dbPath))
                            File.Delete(dbPath);

                        processor.CreateSqliteDB(dbPath, file);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Failed to load Btrieve file {s}: {e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
}
