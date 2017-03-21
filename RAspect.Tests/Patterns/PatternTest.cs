using RAspect.Patterns.Exception;
using RAspect.Patterns.Logging;
using RAspect.Patterns.Threading;
using RAspect.Tests.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Mono.Cecil.Rocks;

namespace RAspect.Tests.Patterns
{
    /// <summary>
    /// Patterns Test
    /// </summary>
    public class PatternTest
    {
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
        public void WillThrowExceptionIfInstanceIsUsedInDifferentThread()
        {
            var obj = new PatternModel();
            Exception ex = null;

            obj.AllowOnlyOneThreadForInstance();

            Task.Run(() => {
                try
                {
                    obj.AllowOnlyOneThreadForInstance();
                }
                catch(Exception e)
                {
                    ex = e;
                }
            }).Wait();

            Assert.NotNull(ex);
        }

        [Fact]
        public void WillThrowExceptionIfMultipleThreadCallsOneMethod()
        {
            var obj = new PatternModel();
            var reset = new ManualResetEvent(false);

            var exCount = 0;

            Task.Run(() => {
                obj.AllowsOnlyOneThread(reset);
            });

            try
            {
                obj.AllowsOnlyOneThread(reset);
            }
            catch(Exception)
            {
                exCount++;
                reset.Set();
            }
            
            Assert.True(exCount > 0);
        }

        [Fact]
        public void WillNotThrowExceptionDueToUnSafeThread()
        {
            int expected = 10;
            var model = new PatternModel(output);
            var count = 0;
            model.TestGeneric(100, 300);
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

        [Fact]
        public void WillLogMethod()
        {
            var backend = new FakeLoggingBackend();
            LoggingManager.Configure(backend);
            var obj = new LoggingObject();
            var result = obj.AddMethod(10, 10);

            Assert.Equal(20, result);

            Assert.True(backend.logs.Any(x => x.Item1.Method.Name == "AddMethod"));
        }

        [Fact]
        public void WillLogErrorMethod()
        {
            var backend = new FakeLoggingBackend();
            LoggingManager.Configure(backend);
            var obj = new LoggingObject();

            Assert.Throws<DivideByZeroException>(() => obj.DivisionError());
            
            Assert.True(backend.logs.Any(x => x.Item4 != null && x.Item4.GetType() == typeof(DivideByZeroException)));
        }

        [Fact]
        public void ShouldLazyLoadProperty()
        {
            var model = new PatternModel();
            var expected = model.RandomInt;
            var value = model.RandomInt;

            Assert.Equal(expected, value);
        }

        [Fact]
        public void ShouldReplaceDateTimeNow()
        {
            var model = new PatternModel();
            var expected = DateTime.Parse("12/5/1985");
            var actual = model.ReplaceDateNow();
            var actual2 = model.ReplaceDateToday();

            Assert.Equal(expected.Month, actual.Month);
            Assert.Equal(expected.Day, actual.Day);
            Assert.Equal(expected.Year, actual.Year);

            Assert.Equal(expected.Month, actual2.Month);
            Assert.Equal(expected.Day, actual2.Day);
            Assert.Equal(expected.Year, actual2.Year);
        }

        [Fact]
        public void ShouldNotFailWithStackOverflow()
        {
            var model = new PatternModel();
            var result = Math.Abs(model.Fib(10000000));
            Assert.True(result > 0);
        }

        [Fact]
        public void ShouldNotThrowExceptionInConstructor()
        {
            var model = new PatternModel();
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
