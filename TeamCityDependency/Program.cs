using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace TeamCityDependency
{

    class Program
    {
        public static String ildasm;
        public static String gnuDiff;
        public static String dumpBin;
        static string ildasmArgs;
        static string dumpBinArgs;
        static string baseDirectory;
        static Dictionary<string, List<string>> map;
        static HashSet<string> hs;

        static void variableSetup()
        {
            var res = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            ildasm = ConfigurationManager.AppSettings["ildasm"];
            dumpBin = ConfigurationManager.AppSettings["dumpBin"];
            ildasmArgs = ConfigurationManager.AppSettings["ildasmArgs"];
            dumpBinArgs = ConfigurationManager.AppSettings["dumpBinArgs"];
            baseDirectory = "C:\\datacore-sds\\GitHub\\datacore-sds\\DataCore\\Executive\\";
            map = new Dictionary<string, List<string>>();
            hs = new HashSet<string>();
        }

        static void systemBinaryLoad()
        {
            string[] lines = System.IO.File.ReadAllLines("SystemBinariesList.txt");
            foreach (string str in lines)
            {
                hs.Add(str);
            }
        }

        static void mapSetup(string baseDirectory)
        {
            var watch = new System.Diagnostics.Stopwatch();
            Console.Write("Map Setup Process Started");
            watch.Start();
            string[] systemFileNames = Directory.GetFiles(baseDirectory, "*.sys", SearchOption.AllDirectories).ToArray();
            string[] exeFileNames = Directory.GetFiles(baseDirectory, "*.exe", SearchOption.AllDirectories).ToArray();
            string[] dllFileNames = Directory.GetFiles(baseDirectory, "*.dll", SearchOption.AllDirectories).ToArray();

            int index = 0;
            foreach (string file in systemFileNames)
            {
                Boolean ild = IsDotNetAssembly(file);
                string fileName = Path.GetFileName(file);
                Console.WriteLine("Working on System file " + index + " " + fileName);
                index++;
                List<string> manifest = GetDisassembly(file);
                parseBinary(fileName, manifest, ild);
            }

            index = 0;
            foreach (string file in exeFileNames)
            {
                Boolean ild = IsDotNetAssembly(file);
                string fileName = Path.GetFileName(file);
                Console.WriteLine("Working on EXE file " + index + " " + fileName);
                index++;
                List<string> manifest = GetDisassembly(file);
                parseBinary(fileName, manifest, ild);
            }

            index = 0;
            foreach (string file in dllFileNames)
            {
                Boolean ild = IsDotNetAssembly(file);
                string fileName = Path.GetFileName(file);
                Console.WriteLine("Working on DLL file " + index + " " + fileName);
                index++;
                List<string> manifest = GetDisassembly(file);
                parseBinary(fileName, manifest, ild);
            }

            watch.Stop();
            Console.WriteLine($"Map Setup Process Ended. Execution Time: {watch.ElapsedMilliseconds} ms");
        }

        //This function will parse different types of binary and get dispendencies from its manifest
        public static void parseBinary(string currentBinary, List<string> manifest, Boolean ild)
        {

            if (hs.Contains(currentBinary)) return;

            foreach (string str in manifest)
            {
                if (ild)
                {
                    if (str.Length > 20 && str.Substring(0, 16).Equals(".assembly extern"))
                    {
                        string assemblyName = str.Substring(17).Trim();


                        if (!assemblyName.Any(char.IsLower))
                        {
                            assemblyName = assemblyName.ToLower();
                        }

                        if (assemblyName[0] == '\'' && assemblyName[assemblyName.Length - 1] == '\'')
                        {
                            assemblyName = assemblyName.Substring(1, assemblyName.Length - 2);
                        }
                        assemblyName = assemblyName + ".dll";

                        if (hs.Contains(assemblyName)) continue;


                        if (!map.ContainsKey(assemblyName))
                        {
                            List<string> newList = new List<string>();
                            map.Add(assemblyName, newList);
                        }
                        List<string> currentList = map[assemblyName];

                        currentList.Add(currentBinary);

                    }
                }
                else
                {
                    if (str.Contains(".exe") || str.Contains(".sys") || str.Contains(".dll") || str.Contains(".EXE") || str.Contains(".DLL") || str.Contains(".SYS"))
                    {
                        if (str.Contains(currentBinary)) continue;

                        string dep = str.Trim();

                        string withoutExtension = dep.Substring(0, dep.Length - 4);
                        if (!withoutExtension.Any(char.IsLower))
                        {
                            dep = dep.ToLower();
                        }

                        if (hs.Contains(dep)) continue;

                        if (!map.ContainsKey(dep))
                        {
                            List<string> newList = new List<string>();
                            map.Add(dep, newList);
                        }
                        List<string> currentList = map[dep];
                        currentList.Add(currentBinary);
                    }
                }
            }
        }

        public static List<string> GetDisassembly(string fileName)
        {
            var fileContents = new List<string>();
            string disassembler = null;
            string disassemblerArgs = null;

            if (fileName == null || fileName == "") return fileContents;

            if (IsDotNetAssembly(fileName))
            {
                disassembler = Program.ildasm;
                disassemblerArgs = Program.ildasmArgs;
            }
            else
            {
                disassembler = Program.dumpBin;
                disassemblerArgs = Program.dumpBinArgs;
            }

            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = disassembler,
                Arguments = disassemblerArgs + " " + "\"" + fileName + "\"",
                RedirectStandardOutput = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    string line;
                    StreamReader reader;

                    using (reader = process.StandardOutput)
                    {
                        while ((line = reader.ReadLine()) != null) fileContents.Add(line);
                    }
                }
            }
            catch (Exception e1)
            {
                Console.WriteLine("Error occured in disassembling");
                Console.WriteLine(e1);
            }
            return fileContents;
        }

        public static bool IsDotNetAssembly(string fileName)
        {
            if (fileName == null || fileName.Length == 0) return false;

            if (!File.Exists(fileName)) return false;

            try
            {
                var testAssm =
                    AssemblyName.GetAssemblyName(fileName);

                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (FileLoadException)
            {
                return false;
            }
        }

        public static void serializeMap(string file)
        {
            BinaryFormatter bf = new BinaryFormatter();

            FileStream fsout = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
            try
            {
                using (fsout)
                {
                    bf.Serialize(fsout, map);
                    Console.WriteLine("Map file successfully saved: " + file);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR IN SERIAIZATION");
                Console.WriteLine(e);
            }
        }


        static void Main(string[] args)
        {


            string[] systemFileNames = Directory.GetFiles(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\", " *.exe", SearchOption.AllDirectories).ToArray();
            foreach(string str in systemFileNames)
            {
                Console.WriteLine(str);
            }








            /*variableSetup();
            systemBinaryLoad();
            mapSetup("TeamCityDependency\\");
            serializeMap("dependency.io");

            *//*string[] lines =
            {
                "First line", "Second line", "Third line"
            };

            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);*//*

            List<string> list = new List<string>();

            int index = 0;
            while (index < args.Length)
            {
                list.Add("Argument no" + (index + 1) + " " + args[index]);
                index++;
            }

            *//*var webRequest = WebRequest.Create(@"http://172.20.0.179/httpAuth/downloadBuildLog.html?buildId=" + args[0]);
            webRequest.Method = "GET";
            webRequest.Headers["Authorization"] = "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("kpujara:Lilyaldrin123"));

            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
            using (var reader = new StreamReader(content))
            {
                while (reader.Peek() >= 0)
                {
                    list.Add(reader.ReadLine());
                }
            }*//*

            List<string> files = getAllChangedFiles(args[1]);
            foreach (string file in files)
            {
                list.Add(file);
            }

            File.WriteAllLinesAsync("WriteLines.txt", list);*/

        }

        public static List<string> getAllChangedFiles(string path)
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            List<string> files = new List<string>();


            foreach (string line in lines)
            {
                int lastIndexSlash = line.LastIndexOf('/') + 1;
                int firstIndexColon = line.IndexOf(":CHANGED");

                if (lastIndexSlash == -1 || firstIndexColon == -1) continue;

                string changedFile = line.Substring(lastIndexSlash, firstIndexColon - lastIndexSlash);
                files.Add(changedFile);    
            }


            return files;
        }


    }

    
}
