using RAspect.Patterns.Exception;
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
        static PatternTest()
        {
            ILWeaver.Weave<FrozenObject>();
            ILWeaver.Weave<PatternModel>();
            ILWeaver.Weave<ImmutableObject>();
            ILWeaver.SaveAssembly();
        }

        [Fact]
        public void CanSwallowException()
        {
            var model = new PatternModel();
            model.CreateException();
        }

        [Fact]
        public void CannotModifyInstanceThatIsImmutable()
        {
            var obj = new ImmutableObject("Test Value");

            Assert.Throws<ObjectReadOnlyException>(() =>
            {
                obj.Name = "New Test Value";
            });
        }

        [Fact]
        public void CanFreezeObject()
        {
            var obj = new FrozenObject();
            obj.Name = "Test Value";

            Freezeable.Freeze(obj);

            Assert.Throws<ObjectReadOnlyException>(() =>
            {
                obj.ID = 10;
            });
        }
    }
}
