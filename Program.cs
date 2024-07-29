using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System.Runtime.InteropServices;
using System.Linq;
using static Mutagen.Bethesda.Plugins.Binary.Processing.BinaryFileProcessor;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System;
using CommandLine;

namespace Spriggit_Helper
{
    public enum OutputFormat
    {
        json,
        yaml
    }

    public class Options
    {
        [Option('s', "source", Required = true, HelpText = "Full path to the source .esp/.esm/.esl file.")]
        public string SourceFile { get; set; }

        [Option('l', "localOnly", Default = false, HelpText = "Use the current directory as the only source for masters.")]
        public bool CurrentDirOnly { get; set; }

        [Option('f', "format", Default = OutputFormat.yaml, HelpText = "Output format")]
        public OutputFormat OutputFormat { get; set; }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            if(result.Errors.Count() > 0)
            {
                Console.WriteLine("Exited with errors!\nPress enter to close this window.");
                Console.ReadKey();
                return;
            }

            Options config = result.Value;

            if (!config.SourceFile.EndsWith(".esp") && !config.SourceFile.EndsWith(".esm") && !config.SourceFile.EndsWith(".esl"))
            {
                Console.WriteLine(config.SourceFile);
                Console.Error.WriteLine("Unsupported filetype.");
                Console.ReadKey();
                return;
            }
            else if (!File.Exists(config.SourceFile))
            {
                Console.Error.WriteLine("Unable to locate file.");
                Console.ReadKey();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.White;
            Console.Write("Spriggit Helper");
            Console.WriteLine(new String(' ', Console.WindowWidth - 15));
            Console.ResetColor();
            
            ModSettings? modSettings = null;

            var sourcePath = Path.GetDirectoryName(config.SourceFile);
            var sourceFile = Path.GetFileName(config.SourceFile);

            if(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".spriggit_helper.json")))
            {
                modSettings = JsonSerializer.Deserialize<ModSettings>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".spriggit_helper.json")));
            }


            using var env = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
            var mod = SkyrimMod.CreateFromBinaryOverlay(config.SourceFile, SkyrimRelease.SkyrimSE);

            var masters = mod.MasterReferences.Select(x => x.Master.FileName.ToString()).Order();

            if (modSettings == null)
                modSettings = new ModSettings();

            if (!File.Exists(modSettings.SpriggitPath))
            {
                var selectPath = ShowDialog("Spriggit.CLI.exe");
                if (String.IsNullOrEmpty(selectPath))
                    return;
                modSettings.SpriggitPath = selectPath;
            }

            if (!config.CurrentDirOnly)
            {
                foreach (var master in masters)
                {
                    if (modSettings.MasterLocations.ContainsKey(master))
                    {
                        if (File.Exists(modSettings.MasterLocations[master]))
                        {
                            continue;
                        }
                    }
                    else if (File.Exists(Path.Combine(env.DataFolderPath, master)))
                    {
                        modSettings.MasterLocations.Add(master, env.DataFolderPath);
                        continue;
                    }
                    else
                    {
                        var selectPath = ShowDialog(master);
                        if (String.IsNullOrEmpty(selectPath))
                            return;

                        if (!modSettings.MasterLocations.ContainsKey(master))
                            modSettings.MasterLocations.Add(master, Path.GetDirectoryName(selectPath) ?? "./");
                        else
                            modSettings.MasterLocations[master] = Path.GetDirectoryName(selectPath) ?? "./";
                        continue;
                    }
                }
            }

            //foreach(var item in modSettings.MasterLocations)
            //{
            //    if(!masters.ToList().Contains(item.Key))
            //        modSettings.MasterLocations.Remove(item.Key);
            //}
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".spriggit_helper.json"), JsonSerializer.Serialize(modSettings, ModSettings.serializerOptions));

            string tempDir = "";
            if (!config.CurrentDirOnly)
            {
                tempDir = GetTemporaryDirectory();
                Console.WriteLine("Temporary Directory: " + tempDir);

                foreach (var item in masters)
                {
                    Console.WriteLine($"Copying {item} to temp directory.");
                    File.Copy(Path.Combine(modSettings.MasterLocations[item], item), Path.Combine(tempDir, item), true);
                }
                Console.WriteLine($"Copying {sourceFile} to temp directory.");
                File.Copy(config.SourceFile, Path.Combine(tempDir, sourceFile));
            }

            Console.Write("\n\n\n");


            Process process = new Process();
            if (!config.CurrentDirOnly)
                if(config.OutputFormat == OutputFormat.yaml)
                    process.StartInfo.Arguments = "serialize -i \"" + Path.Combine(tempDir, sourceFile) + "\" -o \"" + Path.Combine(sourcePath, sourceFile) + ".yaml\" -p Spriggit.yaml -g SkyrimSE";
                else
                    process.StartInfo.Arguments = "serialize -i \"" + Path.Combine(tempDir, sourceFile) + "\" -o \"" + Path.Combine(sourcePath, sourceFile) + ".json\" -p Spriggit.json -g SkyrimSE";
            else
                if (config.OutputFormat == OutputFormat.yaml)
                    process.StartInfo.Arguments = "serialize -i \"" + config.SourceFile + "\" -o \"" + config.SourceFile + ".yaml\" -p Spriggit.yaml -g SkyrimSE";
                else
                    process.StartInfo.Arguments = "serialize -i \"" + config.SourceFile + "\" -o \"" + config.SourceFile + ".json\" -p Spriggit.json -g SkyrimSE";
            process.StartInfo.FileName = modSettings.SpriggitPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            //* Set your output and error (asynchronous) handlers
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            //* Start process and handlers
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();


            // hope this isn't too dangerous
            if (!config.CurrentDirOnly)
            {
                Console.Write("\n\n\n");
                Console.WriteLine("Removing temp directory.");
                Directory.Delete(tempDir, true);
            }

            Console.Write("\n\n");
            if(process.ExitCode != 0)
            {
                Console.WriteLine("Exited with errors!\nPress enter to close this window.");
                Console.ReadKey();
            }
            else
            {
                Console.WriteLine("Done!\nExiting in 1 second.");
                Thread.Sleep(1000);
            }
            return;
        }










        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            Console.WriteLine(outLine.Data);
        }

        public static string GetTemporaryDirectory()
        {
            //string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp_" + Path.GetRandomFileName());

            if (Path.Exists(tempDirectory))
            {
                return GetTemporaryDirectory();
            }
            else
            {
                Directory.CreateDirectory(tempDirectory);
                return tempDirectory;
            }
        }


        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        private static string ShowDialog(string filterName = "")
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            // Define Filter for your extensions (Excel, ...)
            //ofn.lpstrFilter = "Excel Files (*.xlsx)\0*.xlsx\0All Files (*.*)\0*.*\0";
            if (filterName == "")
            {
                ofn.lpstrFilter = "Bethesda Files (.esp, .esm, .esl)\0*.esp;*.esm;*.esl\0\0";
                ofn.lpstrTitle = "Select Mod File...";
            }
            else
            {
                ofn.lpstrFilter = $"{filterName}\0{filterName}\0\0";
                ofn.lpstrTitle = "Please locate " + filterName;
            }
            ofn.lpstrFile = new string(new char[256]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFileTitle = new string(new char[64]);
            ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
            if (GetOpenFileName(ref ofn))
                return ofn.lpstrFile;
            return string.Empty;
        }
    }
}
