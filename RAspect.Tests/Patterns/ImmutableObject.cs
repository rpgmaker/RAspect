using RAspect.Patterns.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Patterns
{
    [ImmutableAttribute]
    public class ImmutableObject
    {
        public ImmutableObject(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
