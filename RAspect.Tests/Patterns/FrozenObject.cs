using RAspect.Patterns.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Patterns
{
    [Freezeable]
    public class FrozenObject
    {
        public string Name { get; set; }
        public int ID { get; set; }
    }
}
