using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Thread = System.Threading;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Base implementation of managing aspects that interact with ReaderWriterLockSlim
    /// </summary>
    public abstract class ReaderWriterBase : AspectBase
    {
        /// <summary>
        /// Get Lock Method
        /// </summary>
        private readonly static MethodInfo GetLockMethod = typeof(ReaderWriterBase).GetMethod("GetLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Dispose Method
        /// </summary>
        private readonly static MethodInfo DisposeMethod = typeof(ReaderWriterBase).GetMethod("Dispose", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Tracker collection for keeping track of ReaderWriterLockSlim instance
        /// </summary>
        private readonly static ConcurrentDictionary<object, Thread.ReaderWriterLockSlim> Locks =
            new ConcurrentDictionary<object, Thread.ReaderWriterLockSlim>();

        /// <summary>
        /// Object references for representing static
        /// </summary>
        private readonly static object StaticInstance = new object();

        /// <summary>
        /// Local Builder for return value
        /// </summary>
        [ThreadStatic]
        private static LocalBuilder local;

        /// <summary>
        /// Local Builder for lock value
        /// </summary>
        [ThreadStatic]
        private static LocalBuilder @lock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderWriterBase"/> class.
        /// </summary>
        public ReaderWriterBase()
        {
            OnBeginAspectBlock = BeginAspectBlock;
            OnEndAspectBlock = EndAspectBlock;
        }

        /// <summary>
        /// Gets Enter Method
        /// </summary>
        internal abstract MethodInfo EnterMethod { get; }

        /// <summary>
        /// Gets Exit Method
        /// </summary>
        internal abstract MethodInfo ExitMethod { get; }

        /// <summary>
        /// Gets weave block type
        /// </summary>
        internal override WeaveBlockType BlockType
        {
            get
            {
                return WeaveBlockType.Wrapping;
            }
        }

        internal static Thread.ReaderWriterLockSlim GetLock(object instance)
        {
            Thread.ReaderWriterLockSlim @lock;

            instance = instance ?? StaticInstance;

            if (!Locks.TryGetValue(instance, out @lock))
            {
                @lock = Locks[instance] = new Thread.ReaderWriterLockSlim();
            }

            return @lock;
        }

        internal static void Dispose(object instance)
        {
            Thread.ReaderWriterLockSlim @lock;

            if (Locks.TryGetValue(instance, out @lock))
            {
                try
                {
                    @lock.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// Aspect code to inject at the beginning of weaved method
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void BeginAspectBlock(MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
            var meth = method as MethodInfo;
            var returnType = meth.ReturnType;
            local = returnType != typeof(void) ? il.DeclareLocal(returnType) : null;
            @lock = il.DeclareLocal(typeof(Thread.ReaderWriterLockSlim));

            il.Emit(method.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, GetLockMethod);
            il.Emit(OpCodes.Stloc, @lock);

            il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldloc, @lock);
            il.Emit(OpCodes.Callvirt, EnterMethod);
        }

        /// <summary>
        /// Aspect code to inject at the end of weaved method
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void EndAspectBlock(MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
            if (local != null)
                il.Emit(OpCodes.Stloc, local);

            il.BeginFinallyBlock();
            
            il.Emit(OpCodes.Ldloc, @lock);
            il.Emit(OpCodes.Callvirt, ExitMethod);
            
            il.EndExceptionBlock();

            if (local != null)
                il.Emit(OpCodes.Ldloc, local);
        }
    }
}
