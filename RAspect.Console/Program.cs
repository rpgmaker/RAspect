using RAspect;
using RAspect.Aspects;
using RAspect.ConsoleApp;
using RAspect.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RAspect.ConsoleApp
{
    public class TestData : Object
    {
        public string Name { get; set; }
        public string ToStringEx()
        {
            return "Hello world String";
        }
        public override string ToString()
        {
            return base.ToString();
        }
    }

    public class S1
    {
        public int ID { get; set; }
        public S1()
        {
            var @this = this;
        }
    }

    public class S2
    {
        public int ID { get; set; }
        public S2()
        {

        }
    }

    public unsafe class Program
    {
        const int COUNT = 100000;
        
        private static void Set(ConstructorInfo ctor)
        {
        }

        public void Ctor()
        {
            object @this = this;
            TestMyClass3 obj = @this as TestMyClass3;
           // obj.ID = 3333;
        }

        unsafe static void Main(string[] args)
        {
            //var tdMethod = typeof(TestData).GetMethod("ToString");
            //var tdsMethod = typeof(TestData).GetMethod("ToStringEx");

            //tdMethod.SwapWith(tdsMethod);

            //var tdata = new TestData();
            //var ttdatas = tdata.ToString();
            //Console.WriteLine(ttdatas);

            //return;
            //ILWeaver.Weave();
            //ILWeaver.SaveAssembly();

            //var ctor = typeof(S2).GetConstructor(Type.EmptyTypes);
            //var ctor2 = typeof(S1).GetConstructor(Type.EmptyTypes);

            var ctor = typeof(TestMyClass3).GetConstructor(Type.EmptyTypes);
            var ctor2 = typeof(TestMyClass).GetConstructor(Type.EmptyTypes);


            var d = (uint*)ctor.MethodHandle.Value.ToPointer();
            var sa = (uint*)ctor2.MethodHandle.Value.ToPointer();

            for(var i = 0; i <= 50; i++)
            {
                Console.WriteLine("d{0}: {1}, sa{0}: {2}", i, *(d + i), *(sa + i));
            }

            var dd = (uint*)ctor.MethodHandle.GetFunctionPointer().ToPointer();
            var ssa = (uint*)ctor2.MethodHandle.GetFunctionPointer().ToPointer();

            *dd = *ssa;
            //*(d + 11) = (uint)ssa;
            //*d = *sa;

            //var v = new S2();

            //return;

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
