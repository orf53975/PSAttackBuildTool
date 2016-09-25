﻿using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Runtime.Serialization.Json;
using PSAttackBuildTool.Modules;
using PSAttackBuildTool.PSAttack;

namespace PSAttackBuildTool.Utils
{
    class PSABTUtils
    {
        public static List<Module> GetModuleList(MemoryStream JSON)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Module>));
            List<Module> moduleList = (List<Module>)serializer.ReadObject(JSON);
            return moduleList;
        }

        public static void BuildCsproj(List<Module> modules, Attack attack, GeneratedStrings generatedStrings)
        {
            attack.ClearCsproj();
            List<string> files = new List<string>();
            foreach (Module module in modules)
            {
                files.Add(CryptoUtils.EncryptString(attack, module.name, generatedStrings));
            }
            PSAttackCSProj csproj = new PSAttackCSProj();
            csproj.Session = new Dictionary<string, object>();
            csproj.Session.Add("files", files);
            csproj.Session.Add("keyStoreFileName", generatedStrings.Store["keyStoreFileName"]);
            csproj.Initialize();

            var generatedCode = csproj.TransformText();
            File.WriteAllText(attack.csproj_file, generatedCode);
        }

        public static void BuildConfigFile(Attack attack, GeneratedStrings generatedStrings)
        {
            attack.ClearConfigFile();
            PSAttackConfig psaConfig = new PSAttackConfig();
            psaConfig.Session = new Dictionary<string, object>();
            psaConfig.Session.Add("encryptionKey", generatedStrings.Store["encryptionKey"]);
            psaConfig.Session.Add("keyStoreFileName", generatedStrings.Store["keyStoreFileName"]);
            psaConfig.Initialize();

            var generatedCode = psaConfig.TransformText();
            File.WriteAllText(attack.config_file, generatedCode);
        }

        public static string DownloadFile(string url, string dest)
        {
            WebClient wc = new WebClient();
            wc.DownloadFile(url, dest);
            return dest;
        }

        public static string UnzipFile(string zipPath)
        {
            if (Directory.Exists(Strings.attackUnzipDir))
            {
                Directory.Delete(Strings.attackUnzipDir, true);
            }
            Directory.CreateDirectory(Strings.attackUnzipDir);
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                archive.ExtractToDirectory(Strings.attackUnzipDir);
                return Path.Combine(Strings.attackUnzipDir, archive.Entries[0].FullName.Replace("/","\\"));
            }
        }

        public static string GetPSAttackBuildToolDir()
        {
            string PSAttackBuildDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PSAttackBuildTool");
            if (!(Directory.Exists(PSAttackBuildDir)))
            {
                Directory.CreateDirectory(PSAttackBuildDir);
            }
            return PSAttackBuildDir+"\\";
        }

       public static Attack GetPSAttack(Uri URL)
        {
            WebClient wc = new System.Net.WebClient();
            // This took a while to figure out: https://developer.github.com/v3/#user-agent-required
            wc.Headers.Add("user-agent", Strings.githubUserAgent);
            string JSON = wc.DownloadString(URL);
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(JSON));
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Attack>));
            List<Attack> psattackReleaseList = (List<Attack>)serializer.ReadObject(stream);
            return psattackReleaseList[0];
        }

        public static int BuildPSAttack(Attack attack, GeneratedStrings generatedStrings)
        {
            //DateTime now = DateTime.Now;
            //string buildDate = String.Format("{0:MMMM dd yyyy} at {0:hh:mm:ss tt}", now);
            //using (StreamWriter buildDateFile = new StreamWriter(Path.Combine(attack.resources_dir, "attackDate.txt")))
            //{
            //    buildDateFile.Write(buildDate);
            //}
            string dotNetDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            string msbuildPath = Path.Combine(dotNetDir, "msbuild.exe");
            if (File.Exists(msbuildPath))
            {
                Process msbuild = new Process();
                msbuild.StartInfo.FileName = msbuildPath;
                msbuild.StartInfo.Arguments = attack.build_args(Path.Combine(Strings.obfuscatedSourceDir, generatedStrings.Store["psaReplacement"] + ".sln"));
                msbuild.StartInfo.UseShellExecute = false;
                msbuild.StartInfo.RedirectStandardOutput = true;
                msbuild.StartInfo.RedirectStandardError = true;

                Console.WriteLine("Running build with this command: {0} {1}", msbuild.StartInfo.FileName, msbuild.StartInfo.Arguments);

                msbuild.Start();
                string output = msbuild.StandardOutput.ReadToEnd();
                Console.WriteLine(output);
                string err = msbuild.StandardError.ReadToEnd();
                Console.WriteLine(err);
                msbuild.WaitForExit();
                int exitCode = msbuild.ExitCode;
                msbuild.Close();
                return exitCode;
            }
            return 999;
        }

        public static string RandomString(int length, Random rand)
        {
            string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            char[] generatedChars = new char[length];
            for (int i = 0; i < length; i++)
            {
                generatedChars[i] = allowedChars[rand.Next(0, allowedChars.Length)];
            }
            return new string(generatedChars);
        }
    }

    class GeneratedStrings
    {
        public Dictionary<string,string> Store { get; set; }

        public GeneratedStrings()
        {
            this.Store = new Dictionary<string, string>();
        }
    }
}
