using RAspect.Patterns;
using RAspect.Patterns.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Tests.Patterns
{
    public class FakeLoggingBackend : LoggingBackend
    {
        public List<Tuple<MethodContext, LoggingType, string, Exception, object[]>> logs =
            new List<Tuple<MethodContext, LoggingType, string, Exception, object[]>>();
        public override void Log(MethodContext context, LoggingType logType, string message, Exception ex, params object[] args)
        {
            logs.Add(new Tuple<MethodContext, LoggingType, string, Exception, object[]>(context, logType, message, ex, args));
        }
    }

    public class LoggingObject
    {
        [Log]
        public int AddMethod(int x, int y)
        {
            return x + y;
        }

        [Log]
        public void DivisionError()
        {
            var num = 100;
            var value = num / 0;
        }
    }
}
