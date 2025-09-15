// <copyright file="Program.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace Launcher
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Program
    {
        private static string ReadPassword()
        {
            var pass = string.Empty;
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    Console.Write("\b \b");
                    pass = pass[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    pass += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);

            Console.WriteLine();
            return pass;
        }

        private static async Task Main(string[] args)
        {
            if (!GameHelperFinder.TryFindGameHelperExe(out var gameHelperDir, out var gameHelperLoc))
            {
                Console.WriteLine($"GameHelper.exe is also not found in {gameHelperDir}");
                Console.ReadKey();
                return;
            }

            /*var disableOcFile = Path.Join(gameHelperDir, "DISABLE_OC_DOWNLOAD");
            var ocUsernameFile = Path.Join(gameHelperDir, "OC_USERNAME");
            var disableOcFileExists = File.Exists(disableOcFile);
            var ocUsernameFileExists = File.Exists(ocUsernameFile);
            if (!(disableOcFileExists || ocUsernameFileExists))
            {
                Console.WriteLine("Saying yes on the following question will require your ownedcore username & password.");
                Console.WriteLine("Saying no on the following question will still inform you " +
                    "(via Windows Notification system) if there is a new version available.");
                Console.Write("Do you want to auto upgrade GameHelper from ownedcore website? (y/n):");
                if (Console.ReadLine().ToLowerInvariant().StartsWith("y"))
                {
                    Console.Write("Please provide your OC Username:");
                    File.WriteAllText(ocUsernameFile, Console.ReadLine().Trim());
                    ocUsernameFileExists = true;
                    Console.WriteLine($"Created {ocUsernameFile} file to save your username.");
                }
                else
                {
                    File.Create(disableOcFile).Close();
                    disableOcFileExists = true;
                    Console.WriteLine($"Created {disableOcFile} file to save your response.");
                }
            }

            var isWaiting = false;
            var passwordMd5 = string.Empty;

            if (args.Length == 0)
            {
                Console.Write("Are you waiting for a new GameHelper release? (y/n):");
                isWaiting = Console.ReadLine().ToLowerInvariant().StartsWith("y");
            }

            if (isWaiting && ocUsernameFileExists )
            {
                Console.Write("Provide ownedcore password:");
                passwordMd5 = string.Concat(MD5.HashData(Encoding.UTF8.GetBytes(ReadPassword())).Select(x => x.ToString("x2")));
            }

            do
            {
                var (isNewVersionFound, downloadUrl) = await AutoUpdate.IsNewVersionFoundAsync(gameHelperDir);
                if (isNewVersionFound)
                {
                    Console.WriteLine("New gamehelper version found.");
                    if (disableOcFileExists)
                    {
                        Console.WriteLine("Please download it from ownedcore website.");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        break;
                    }
                    else if (ocUsernameFileExists)
                    {
                        if (string.IsNullOrEmpty(passwordMd5))
                        {
                            Console.Write("Provide ownedcore password for auto upgrade:");
                            passwordMd5 = string.Concat(MD5.HashData(Encoding.UTF8.GetBytes(ReadPassword())).Select(x => x.ToString("x2")));
                        }

                        if (await AutoUpdate.UpgradeGameHelper(gameHelperDir, downloadUrl, File.ReadAllText(ocUsernameFile), passwordMd5))
                        {
                            // Returning because Launcher should auto-restart on exit.
                            return;
                        }
                        else
                        {
                            Console.WriteLine("Failed to download gamehelper new version. Please download it manually.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey();
                            break;
                        }
                    }
                }
                else if (isWaiting)
                {
                    Console.WriteLine("Didn't find any new version, sleeping for 5 mins....");
                    Thread.Sleep(5 * 60 * 1000);
                }
            }
            while (isWaiting);
            */
            try
            {
                Console.WriteLine("\n\nPreparing GameHelper...");
                var newName = MiscHelper.GenerateRandomString();
                TemporaryFileManager.Purge();
                //TODO: if functionality extends, should probably utilize an argument parser, but good for now
                if (!LocationValidator.IsGameHelperLocationGood(out var message))
                {
                    Console.WriteLine(message);
                    Console.Write("Press any key to ignore this warning.");
                    Console.ReadLine();
                }

                var gameHelperPath = GameHelperTransformer.TransformGameHelperExecutable(gameHelperDir, gameHelperLoc, newName);
                Console.WriteLine($"Starting GameHelper at '{gameHelperPath}'...");
                Process.Start(gameHelperPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch GameHelper due to: {ex}");
                Console.ReadKey();
            }

            return;
        }
    }
}
