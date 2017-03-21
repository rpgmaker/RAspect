using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Compiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = args[0];
            var directory = new FileInfo(path).Directory;

            Environment.CurrentDirectory = directory.FullName;
            
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                var name = new AssemblyName(e.Name);
                var filePath = Path.Combine(Environment.CurrentDirectory, name.Name + ".dll");

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var ms = new MemoryStream())
                    {
                        fs.CopyTo(ms);
                        return Assembly.Load(ms.ToArray());
                    }
                }
            };
            var sw = new Stopwatch();
            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine("RAspect Weaving: {0}", path);

            sw.Start();

            var weaver = new CecilWeaver(path, message => Console.WriteLine(message));
            weaver.Process();
            sw.Stop();

            Console.WriteLine("Completed in {0} second(s)", sw.ElapsedMilliseconds / 1000.0d);
            Console.WriteLine("-----------------------------------------------------------------------------------");
            Environment.ExitCode = 0;
        }
    }
}
