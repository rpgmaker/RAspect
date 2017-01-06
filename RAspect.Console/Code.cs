using RAspect;
using RAspect.Aspects;
using RAspect.ConsoleApp;
using RAspect.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

//[assembly: CustomAspect(SearchTypePattern = "TestMyClass3", Target = WeaveTargetType.Methods)]
//[assembly: Required(SearchTypePattern = "TestMyClass3", SearchMemberPattern = "set_")]
[assembly: PropertyAspect(SearchTypePattern = "TestMyClass3")]
[assembly: EventIntercept(SearchTypePattern = "TestMyClass3")]


namespace RAspect.ConsoleApp
{
    public class EventIntercept : EventInterceptionAspect
    {
        public override void OnAddHandler(EventContext context)
        {
            Console.WriteLine("Add Handler");
        }

        public override void OnInvokeEvent(EventContext context)
        {
            Console.WriteLine("Invoked Event: {0}, {1}", context.Arguments[0], context.Arguments[1]);
        }
    }
    public class CustomAspect : OnMethodBoundaryAspect
    {
        public override void OnEnter(MethodContext context)
        {
            //var name = context.Method.Name;
            //var arguments = context.Arguments;
            //var @return = context.Returns;
        }

        public override void OnLeave(MethodContext context)
        {
            //var name = context.Method.Name;
            //var arguments = context.Arguments;
            //var @return = context.Returns;
        }
    }

    public class PropertyAspect : MemberInterceptionAspect
    {
        static Random _rand = new Random();

        public override void OnGetValue(MemberContext context)
        {
            //context.Value = _rand.Next(50000, 3000000);
        }

        public override void OnSetValue(MemberContext context)
        {
            //context.Value = _rand.Next(10000, 3000000);
        }
    }
    public class Complex
    {
        public int ID { get; set; }

        public List<Complex> List { get; set; }
    }

    public class TestMyClass
    {
        public void Test(int x, int y, Complex c)
        {
            Test2();
        }

        public void Test2()
        {
            Test3();
        }

        public void Test3()
        {
            Test4();
        }

        public void Test4()
        {
            Test5();
        }

        public void Test5()
        {

        }
    }



    public class TestMyClass3
    {
        //private int x = 10, y;
        //[CustomAspect]
        public void Test(int x, int y, Complex c)
        {
            //this.x = 100;
            //this.y = this.x;
            //var list = new List<int> { 1, 2, 3 };
            //var item = list.Where(xx => xx > 10).FirstOrDefault();
            Test2();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TestParameterMethod([EmailAddress]string value)
        {
            Console.WriteLine(value);
            if (myEvent != null)
                myEvent("This is a test", new EventArgs());
        }

        public int ID = 100;

        public event EventHandler myEvent;

        public string Value { get; set; }

        //[CustomAspect]
        public void Test2()
        {
            Test3();
        }

        //[CustomAspect]
        public void Test3()
        {
            Test4();
        }

        //[CustomAspect]
        public void Test4()
        {
            Test5();
        }

        //[CustomAspect]
        public void Test5()
        {

        }
    }
    class Code
    {
    }
}
