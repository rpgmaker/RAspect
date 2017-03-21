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
        private string name;
        public ImmutableObject(string name)
        {
            this.name = name;
        }

        public string Name { get { return name; } set { name = value; } }
    }
}
