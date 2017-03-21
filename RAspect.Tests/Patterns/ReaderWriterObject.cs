using RAspect.Patterns.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Patterns
{
    [ReaderWriterSynchronized]
    public class ReaderWriterObject
    {
        private List<int> Values { get; set; } = new List<int>();
        private static Random rand = new Random();
        
        [Reader]
        public int GetValue()
        {
            var value = rand.Next(0, Values.Count);
            Values.Add(value);
            var v = Values.FirstOrDefault(x => x >= value * 2);
            return value;
        }

        [Writer]
        public void Add(int value, int factor = 10)
        {
            var num = value * factor;
            Values.Add(num);
        }
    }
}
