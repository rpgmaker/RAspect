using RAspect.Patterns.Exception;
using RAspect.Patterns.Threading;
using RAspect.Tests.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

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
            ILWeaver.Weave<ReaderWriterObject>();
            //ILWeaver.SaveAssembly();
        }

        private ITestOutputHelper output = null;
        public PatternTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void CanSwallowException()
        {
            var model = new PatternModel();
            model.CreateException();
        }

        [Fact]
        public void ShouldFailWeaveForReaderWriterUnSync()
        {
            Assert.Throws<ThreadingValidationException>(() =>
                ILWeaver.Weave<ReaderWriterUnSyncObject>());
        }

        [Fact]
        public void ShouldNotThrowExceptionForAccessingCollectionInMultipleThread()
        {
            var obj = new ReaderWriterObject();
            var factor = 2;

            var tasks = new List<Task>();

            System.Exception ex = null;

            try
            {
                tasks.AddRange(ExecuteOnThreads(100, () =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        obj.Add(i, factor);
                    }
                }));

                tasks.AddRange(ExecuteOnThreads(100, () =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                       //var value = obj.GetValue();
                    }
                }));

                Task.WaitAll(tasks.ToArray());
            }catch(System.Exception e)
            {
                ex = e;
            }

            Assert.Null(ex);
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
        public void WillNotThrowExceptionDueToUnSafeThread()
        {
            int expected = 10;
            var model = new PatternModel(output);
            var count = 0;
            Task.WaitAll(ExecuteOnThreads(expected, () => model.NumberThreadUnSafe(ref count)));
            Assert.Equal(expected, count);
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
        
        private Task[] ExecuteOnThreads(int threads, Action action)
        {
            var tasks = new Task[threads];

            for (var i = 0; i < threads; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                action());
            }

            return tasks;
        }
    }
}
