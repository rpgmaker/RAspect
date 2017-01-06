using RAspect.Patterns.Threading;
using RAspect.Tests.Patterns;
using System;
using Xunit;

namespace RAspect.Patterns.Tests
{
    /// <summary>
    /// Patterns Test
    /// </summary>
    public class PatternTest
    {
        public PatternTest()
        {
            ILWeaver.Weave<FrozenObject>();
        }

        [Fact]
        public void CanFreezeObject()
        {
            var obj = new FrozenObject();
            obj.Name = "Test Value";

            Freezeable.Freeze(obj);

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                obj.ID = 10;
            });
        }
    }
}
