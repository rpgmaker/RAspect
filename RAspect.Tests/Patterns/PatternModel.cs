using RAspect.Patterns;
using RAspect.Patterns.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Patterns
{
    public class PatternModel
    {
        private static int syncValue = 1;

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
        public void DivideNumberByZeroThreadUnSafe()
        {
            if (syncValue != 0)
            {
                var x = 2 / syncValue;
            }

            syncValue = 0;
        }
    }
}
