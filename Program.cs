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

namespace Spriggit_Helper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("No file specified.");
                Console.ReadKey();
                return;
            }
            else if (!args[0].EndsWith(".esp") && !args[0].EndsWith(".esm") && !args[0].EndsWith(".esl"))
            {
                Console.WriteLine(args[0]);
                Console.Error.WriteLine("Unsupported filetype.");
                Console.ReadKey();
                return;
            }
            else if (!File.Exists(args[0]))
            {
                Console.Error.WriteLine("Unable to locate file.");
                Console.ReadKey();
                return;
            }

            
            ModSettings? modSettings = null;

            var sourcePath = Path.GetDirectoryName(args[0]);
            var sourceFile = Path.GetFileName(args[0]);

            if(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".spriggit_helper.json")))
            {
                modSettings = JsonSerializer.Deserialize<ModSettings>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".spriggit_helper.json")));
            }


            using var env = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
            var mod = SkyrimMod.CreateFromBinaryOverlay(args[0], SkyrimRelease.SkyrimSE);

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

            foreach (var master in masters)
            {
                if(modSettings.MasterLocations.ContainsKey(master))
                {
                    if (File.Exists(modSettings.MasterLocations[master]))
                    {
                        continue;
                    }
                }
                else if(File.Exists(Path.Combine(env.DataFolderPath, master)))
                {
                    modSettings.MasterLocations.Add(master, env.DataFolderPath);
                    continue;
                }
                else
                {
                    var selectPath = ShowDialog(master);
                    if (String.IsNullOrEmpty(selectPath))
                        return;

                    if(!modSettings.MasterLocations.ContainsKey(master))
                        modSettings.MasterLocations.Add(master, Path.GetDirectoryName(selectPath) ?? "./");
                    else
                        modSettings.MasterLocations[master] = Path.GetDirectoryName(selectPath) ?? "./";
                    continue;
                }
            }

            //foreach(var item in modSettings.MasterLocations)
            //{
            //    if(!masters.ToList().Contains(item.Key))
            //        modSettings.MasterLocations.Remove(item.Key);
            //}
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".spriggit_helper.json"), JsonSerializer.Serialize(modSettings, ModSettings.serializerOptions));

            var tempDir = GetTemporaryDirectory();
            Console.WriteLine("Temporary Directory: " + tempDir);

            foreach(var item in masters)
            {
                File.Copy(Path.Combine(modSettings.MasterLocations[item], item), Path.Combine(tempDir, item), true);
            }
            File.Copy(args[0], Path.Combine(tempDir, sourceFile));



            Process process = new Process();
            process.StartInfo.Arguments = "serialize -i \"" + Path.Combine(tempDir, sourceFile) + "\" -o \"" + Path.Combine(sourcePath, sourceFile) + ".yaml\" -p Spriggit.yaml -g SkyrimSE";
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
            Directory.Delete(tempDir, true);

            Console.WriteLine("\n\n\n\n");
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
