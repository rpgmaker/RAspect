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
        /// Local Builder for lock value
        /// </summary>
        [ThreadStatic]
        private static Mono.Cecil.Cil.VariableDefinition @lock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderWriterBase"/> class.
        /// </summary>
        public ReaderWriterBase()
        {
            OnBeginBlock = BeginBlock;
            OnEndBlock = EndBlock;
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

        public static Thread.ReaderWriterLockSlim GetLock(object instance)
        {
            Thread.ReaderWriterLockSlim @lock;

            instance = instance ?? StaticInstance;

            if (!Locks.TryGetValue(instance, out @lock))
            {
                @lock = Locks[instance] = new Thread.ReaderWriterLockSlim();
            }

            return @lock;
        }

        public static void Dispose(object instance)
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
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void BeginBlock(Mono.Cecil.TypeDefinition typeBuilder, Mono.Cecil.MethodDefinition method, Mono.Cecil.ParameterDefinition parameter, Mono.Cecil.Cil.ILProcessor il)
        {
            var meth = method;
            var returnType = meth.ReturnType.ReflectionType();
            @lock = il.DeclareLocal(typeof(Thread.ReaderWriterLockSlim));

            il.Emit(method.IsStatic ? Mono.Cecil.Cil.OpCodes.Ldnull : Mono.Cecil.Cil.OpCodes.Ldarg_0);
            il.Emit(Mono.Cecil.Cil.OpCodes.Call, GetLockMethod);
            il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, @lock);

            il.BeginExceptionBlock();
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, @lock);
            il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, EnterMethod);
        }

        /// <summary>
        /// Aspect code to inject at the end of weaved method
        /// </summary>
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void EndBlock(Mono.Cecil.TypeDefinition typeBuilder, Mono.Cecil.MethodDefinition method, Mono.Cecil.ParameterDefinition parameter, Mono.Cecil.Cil.ILProcessor il)
        {
            il.BeginFinallyBlock();
            
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, @lock);
            il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, ExitMethod);
            
            il.EndExceptionBlock();
        }
    }
}
