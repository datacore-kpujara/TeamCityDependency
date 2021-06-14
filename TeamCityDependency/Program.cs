using System;
using System.IO;

namespace TeamCityDependency
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] lines =
            {
                "First line", "Second line", "Third line"
            };

            File.WriteAllLinesAsync("WriteLines.txt", lines);

        }
    }
}
