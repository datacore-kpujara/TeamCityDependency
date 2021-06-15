using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace TeamCityDependency
{
    class Program
    {
        static void Main(string[] args)
        {
            /*string[] lines =
            {
                "First line", "Second line", "Third line"
            };

            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);*/

           

            List<string> list = new List<string>();

            var webRequest = WebRequest.Create(@"http://172.20.0.179/httpAuth/downloadBuildLog.html?buildId=324080");
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
            }

            File.WriteAllLinesAsync(AppDomain.CurrentDomain.BaseDirectory + "\\WriteLines.txt", list);

        }
    }
}
