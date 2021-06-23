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
using Newtonsoft.Json;
using System.Net.Mail;
using System.Threading;
using System.Net.Http;
using Newtonsoft.Json.Linq;

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
        static HashSet<string> affectedSystemFiles;

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
            affectedSystemFiles = new HashSet<string>();
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

            if (hs.Contains(currentBinary) || currentBinary.StartsWith("Microsoft") || currentBinary.StartsWith("DevExpress")) return;

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

        static void parseBuildLog(string argument)
        {

            Boolean BuildDatacoreStarted = false;
            Boolean BuildDatacoreCompleted = false;

            List<string> list = new List<string>(); 
            while (!BuildDatacoreCompleted && !BuildDatacoreStarted)
            {
                list = new List<string>();
                var webRequest = WebRequest.Create(@"http://172.20.0.179/httpAuth/downloadBuildLog.html?buildId=" + argument);
                webRequest.Method = "GET";
                webRequest.Headers["Authorization"] = "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("kpujara:Lilyaldrin123"));

                using (var response = webRequest.GetResponse())
                using (var content = response.GetResponseStream())
                using (var reader = new StreamReader(content))
                {
                    while (reader.Peek() >= 0)
                    {
                        list.Add(reader.ReadLine());
                        if(!BuildDatacoreStarted && list[list.Count - 1].Contains("Build DataCore"))
                        {
                            BuildDatacoreStarted = true;
                        }

                        if(BuildDatacoreStarted && list[list.Count - 1].Contains("Process exited with code 0"))
                        {
                            BuildDatacoreCompleted = true;
                        }
                    }
                }
            }

            string[] lines = list.ToArray();

            //string[] lines = System.IO.File.ReadAllLines(logFile);
            int index = 0;
            while (index != lines.Length)
            {
                if (lines[index].Contains("link.exe"))
                {
                    string currentLine = lines[index].Substring(lines[index].IndexOf("/OUT"));
                    string path = currentLine.Substring(0, currentLine.IndexOf(' '));

                    int endIndex = path.Length - 3;
                    int startIndex = endIndex;
                    while (path[startIndex] != '\\')
                    {
                        startIndex--;
                    }

                    string fileName = path.Substring(startIndex + 1, endIndex - startIndex + 1);

                    index++;
                    while (lines[index].EndsWith(".obj"))
                    {
                        string objFile = lines[index].Substring(lines[index].LastIndexOf('\\') + 1);
                        Console.WriteLine("Working on object file " + objFile);

                        if (!map.ContainsKey(objFile))
                        {
                            List<string> nlist = new List<string>();
                            map.Add(objFile, nlist);
                        }

                        List<string> currentList = map[objFile];
                        currentList.Add(fileName);
                        index++;
                    }
                }
                index++;
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

                if (changedFile.EndsWith(".cs"))
                {
                    string filePath = line.Substring(0, firstIndexColon);
                    string[] text = System.IO.File.ReadAllLines(filePath);
                    foreach (string str in text)
                    {
                        if (str.Contains("namespace"))
                        {
                            Console.WriteLine("Found the namespace-> " + str);
                            int index = str.IndexOf("namespace");
                            string nspace = str.Substring(index + 10);

                            changedFile = nspace + "." + changedFile;

                            break;
                        }
                    }
                }

                files.Add(changedFile);
            }
            return files;
        }

        public static void generateJSON(string binary)
        {
            hs = new HashSet<string>();
            if (binary.EndsWith(".cs") || binary.EndsWith(".cpp") || binary.EndsWith(".c"))
            {
                string oldB = binary;

                if (binary.EndsWith(".cs"))
                {
                    binary = binary.Substring(0, binary.Length - 3) + ".dll";
                }
                else if (binary.EndsWith(".c"))
                {
                    binary = binary.Substring(0, binary.Length - 2) + ".obj";
                }
                else if (binary.EndsWith(".cpp"))
                {
                    binary = binary.Substring(0, binary.Length - 4) + ".obj";
                }
            }

            Dependency d = BuildDependencyClass(binary);
            String jsonStr = JsonConvert.SerializeObject(d);
            File.WriteAllText(binary+".json", jsonStr);
        }

        public static void sendMail(HashSet<string> systemFiles, List<string> buildInformation)
        {

            Console.WriteLine("Reached Mail Line");

            MailMessage mail = new MailMessage();
            SmtpClient SmtpServer = new SmtpClient("smtp-mail.outlook.com");

            mail.From = new MailAddress("teamcity@datacore.com");
            mail.To.Add("khushalpujara@gmail.com");
            mail.Subject = "System Files Affected";

            mail.Body = "Build Information:";
            foreach(String str in buildInformation)
            {
                mail.Body += str + "\n";
            }

            mail.Body = "\n The following drivers need to be re-tested \n";
            foreach(String s in systemFiles)
            {
                mail.Body += s + "\n";
            }

            SmtpServer.Port = 587;
            SmtpServer.Credentials = new System.Net.NetworkCredential("teamcity@datacore.com", ".b=JWU6KdRTq2B*_");
            SmtpServer.EnableSsl = true;

            SmtpServer.Send(mail);
        }

        public static Dependency BuildDependencyClass(string baseDLL)
        {
            List<Dependency> dList = new List<Dependency>();
            Dependency d = new Dependency()
            {
                name = baseDLL,
                affects = dList
            };

            hs.Add(baseDLL);

            if (!map.ContainsKey(baseDLL))
            {
                return d;
            }
            List<string> dependants = map[baseDLL];
            foreach (string str in dependants)
            {
                if (str.EndsWith(".sys"))
                {
                    affectedSystemFiles.Add(str + " <- " + baseDLL);
                }

                if (!hs.Contains(str))
                {
                    Dependency dNew = BuildDependencyClass(str);
                    d.affects.Add(dNew);
                }
            }

            return d;
        }

        static void sendSystemFileMails(List<string> buildInformation)
        {
            if(affectedSystemFiles.Count != 0)
            {
                sendMail(affectedSystemFiles, buildInformation);
            }
        }

        public static List<string> getBuildInformation(string argument)
        {


            List<string> list = new List<string>();
            var webRequest = (HttpWebRequest)WebRequest.Create(@"http://172.20.0.179/httpAuth/app/rest/builds/id:" + argument);

            webRequest.Method = "GET";
            webRequest.Accept = "application/json";
            webRequest.Headers["Authorization"] = "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("kpujara:Lilyaldrin123"));


            var response = webRequest.GetResponse();
            var content = response.GetResponseStream();
            var reader = new StreamReader(content).ReadToEnd();

            List<string> buildInformation = new List<String>();
            
            dynamic data = JObject.Parse(reader);

            Console.WriteLine(data);

            buildInformation.Add((string) "Build ID : " + data["id"]);
            buildInformation.Add((string)"Build Type ID : " + data["buildTypeId"]);
            buildInformation.Add((string)"Build Number : " + data["number"]);
            buildInformation.Add((string)"Branch Name : " + data["branchName"]);

            if(data["triggered"]["type"] == "vcs")
            {
                buildInformation.Add((string)"Triggered By : VCS");
            }
            else
            {
                buildInformation.Add((string)"Triggered By : " + data["triggered"]["user"]["username"]);
            }

            return buildInformation;
        }

        static void Main(string[] args)
        {
            variableSetup();
            systemBinaryLoad();
            mapSetup("Datacore\\");
            parseBuildLog(args[0]);
            List<string> files = getAllChangedFiles(args[1]);
            files.Add("PhysicalDisk.cpp");
            File.WriteAllLinesAsync("ChangedFiles.txt", files);
            foreach (string file in files)
            {
                generateJSON(file);
            }
            foreach (String s in affectedSystemFiles)
            {
                Console.WriteLine(s);
            }
            List<string> buildInformation = getBuildInformation(args[1]);
            sendSystemFileMails(buildInformation);

 
        }
    }


    public class Dependency
    {
        public string name { get; set; }
        public List<Dependency> affects { get; set; }
    }




}
