using RAspect;
using RAspect.Aspects;
using RAspect.ConsoleApp;
using RAspect.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RAspect.ConsoleApp
{
    class Program
    {
        const int COUNT = 100000;
        
        unsafe static void Main(string[] args)
        {
            ILWeaver.Weave();
            ILWeaver.SaveAssembly();

            var ctor = typeof(TestMyClass3).GetConstructor(Type.EmptyTypes);
            var ctor2 = typeof(TestMyClass).GetConstructor(Type.EmptyTypes);
            
            var d = (uint*)ctor.MethodHandle.Value.ToPointer();
            var sa = (uint*)ctor2.MethodHandle.Value.ToPointer();

            *d = *sa;

            var tobj = new TestMyClass3();
            tobj.Value = "test.email@gmail.com";
            var evt = new EventHandler((s, e) => { Console.WriteLine("Simple Delegate"); });
            tobj.myEvent += evt;
            tobj.TestParameterMethod(tobj.Value);



            Console.ReadLine();
            return;

            TestPerf("RAspect-Empty Method", () =>
            {
                var obj = new TestMyClass3();
                obj.Test(10, 10, new Complex { ID = 100 });
                obj.Test(10, 10, new Complex { ID = 100 });
            });

            TestPerf("Empty Method", () =>
            {
                var obj = new TestMyClass();
                obj.Test(10, 10, new Complex { ID = 100 });
                obj.Test(10, 10, new Complex { ID = 100 });
            });
             
            System.Console.Read();
        }

        static void TestPerf(string name, Action action)
        {
            action();
            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < COUNT; i++)
            {
                action();
            }
            sw.Stop();

            System.Console.WriteLine("{0} - Completed {1} iterations in {2} Millisecond(s)", name, COUNT, sw.ElapsedMilliseconds);
        }
    }
}
