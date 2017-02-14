using RAspect.Patterns;
using RAspect.Patterns.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace RAspect.Tests.Patterns
{
    public class DateTimeReplacement
    {
        public static DateTime Now
        {
            get
            {
                return DateTime.Parse("12/5/1985");
            }
        }
    }

    public class DateTimeTodayReplacement
    {
        public static DateTime Today
        {
            get
            {
                return DateTime.Parse("12/5/1985");
            }
        }
    }

    public class PatternModel
    {
        static Random rand = new Random();
        ITestOutputHelper output = null;
        public PatternModel()
        {

        }

        public PatternModel(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        [SwallowException]
        public int CreateException()
        {
            var value = 100;
            value = value / 0;
            return value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [ThreadSafe]
        public void NumberThreadUnSafe(ref int count)
        {
            var c = count;
            c++;
            Thread.Sleep(1);
            count = c;
        }

        [AutoLazy]
        public int RandomInt
        {
            get
            {
                return rand.Next(10, 10000);
            }
        }

        [StaticMethodReplacer(typeof(DateTimeReplacement))]
        public DateTime ReplaceDateNow()
        {
            return DateTime.Now;
        }

        [StaticMethodReplacer(typeof(DateTimeTodayReplacement))]
        public DateTime ReplaceDateToday()
        {
            return DateTime.Today;
        }

        [Tail]
        public int Fib(int i, int acc = 0)
        {
            return 0;
            //if (i == 0)
            //{
            //    return acc;
            //}
            //return Fib(i - 1, acc + i);
        }
    }
}
