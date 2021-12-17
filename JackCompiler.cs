using JackAnalyzer;
using System;
using System.IO;

namespace JackCompiler
{
    internal class JackCompiler
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    if (Directory.Exists(args[0]))
                    {
                        ProcessDirectory(args[0]);
                    }
                    else
                    {
                        new CompilationEngine(args);
                    }
                }
                else
                {
                    ProcessDirectory(Directory.GetCurrentDirectory());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void ProcessDirectory(string directory)
        {
            new CompilationEngine(Directory.GetFiles(directory, "*.jack"));
        }
    }
}
