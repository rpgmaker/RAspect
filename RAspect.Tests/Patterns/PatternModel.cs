using RAspect.Patterns;
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        [SwallowException]
        public int CreateException()
        {
            var value = 100;
            value = value / 0;
            return value;
        }
    }
}
