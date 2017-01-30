using RAspect.Patterns.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Patterns
{
    [ReaderWriterSynchronized]
    public class ReaderWriterUnSyncObject
    {
        public int ID { get; set; }

        public void UpdateID(int value)
        {
            ID = value * value / 1;
        }
    }
}
