using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAspect.Patterns.Exception;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;
using System.Threading;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied on a type, ensure that the instance of this can only be accessed by the thread that created the instance. When a different thread accesses instance of this type, a <see cref="ThreadMismatchException" /> exception is thrown.
    /// </summary>
    public sealed class ThreadAffinityAttribute : AspectBase
    {
        /// <summary>
        /// Instances thread tracker
        /// </summary>
        private static ConcurrentDictionary<object, int> instanceThreads =
            new ConcurrentDictionary<object, int>();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadAffinityAttribute"/> class.
        /// </summary>
        public ThreadAffinityAttribute()
        {
            OnBeginBlock = BeginBlock;
        }

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

        /// <summary>
        /// Track thread for instance
        /// </summary>
        /// <param name="instance">Instance</param>
        public static void SetInstanceThread(object instance)
        {
            instance = instance ?? string.Empty;
            instanceThreads[instance] = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Throws exception if instance thread does not match current thread
        /// </summary>
        /// <param name="instance"></param>
        public static void ThrowIfInstanceThreadNotMatch(object instance)
        {
            instance = instance ?? string.Empty;
            var threadID = Thread.CurrentThread.ManagedThreadId;
            var instanceThreadID = instanceThreads[instance];

            if(threadID != instanceThreadID)
            {
                throw new ThreadMismatchException();
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
            if (method.IsStatic)
            {
                return;
            }

            if (method.IsConstructor)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                il.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(ThreadAffinityAttribute).GetMethod("SetInstanceThread"));
                return;
            }

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            il.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(ThreadAffinityAttribute).GetMethod("ThrowIfInstanceThreadNotMatch"));
        }
    }
}
