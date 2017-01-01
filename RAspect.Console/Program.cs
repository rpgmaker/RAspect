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

        private static event EventHandler myEvent;

        private static event EventHandler myEventBackUp;

        private static event EventHandler MyEvent
        {
            add
            {
                myEventBackUp += value;
                myEvent = myEventBackUp;
            }
            remove
            {
                myEventBackUp -= value;
                myEvent = myEventBackUp;
            }
        }
        
        static void Main(string[] args)
        {
            ILWeaver.OptimizeEmptyAspects = true;
            ILWeaver.GenerateAssembly = true;
            ILWeaver.Weave();
            ILWeaver.SaveAssembly();

            var tobj = new TestMyClass3();
            tobj.Value = "Ok this is working";
            var evt = new EventHandler((s, e) => { Console.WriteLine("Simple Delegate"); });
            tobj.myEvent += evt;
            tobj.TestParameterMethod(tobj.Value);

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
