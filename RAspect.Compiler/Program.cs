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
                return Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, name.Name + ".dll"));
            };
            
            var weaver = new CecilWeaver(path);
            weaver.Process();
            Environment.ExitCode = 0;
        }
    }
}
