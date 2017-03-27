using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied to type, ensure access to the methods and properties is synchronized 
    /// </summary>
    public sealed class ThreadSafeAttribute : AspectBase
    {
        /// <summary>
        /// Monitor Enter Method
        /// </summary>
        private readonly static MethodInfo EnterMethod = typeof(Monitor).GetMethod("Enter", new[] { typeof(object), typeof(bool).MakeByRefType() });

        /// <summary>
        /// Monitor Exit Method
        /// </summary>
        private readonly static MethodInfo ExitMethod = typeof(Monitor).GetMethod("Exit", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Monitor Exit Method
        /// </summary>
        private readonly static MethodInfo GetLockObjectMethod = typeof(ThreadSafeAttribute).GetMethod("GetLockObject", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Collection of lock objects
        /// </summary>
        private readonly static ConcurrentDictionary<object, object> LockObjects = new ConcurrentDictionary<object, object>();

        /// <summary>
        /// Local Builder for monitor.enter
        /// </summary>
        [ThreadStatic]
        private static Mono.Cecil.Cil.VariableDefinition lockWasTokenLocal;

        /// <summary>
        /// Local Builder for monitor.enter
        /// </summary>
        [ThreadStatic]
        private static Mono.Cecil.Cil.VariableDefinition tempLocal;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadSafeAttribute"/> class.
        /// </summary>
        public ThreadSafeAttribute()
        {
            OnBeginBlock = BeginBlock;
            OnEndBlock = EndBlock;
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
        /// Get lock object if needed
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <returns></returns>
        public static object GetLockObject(object instance)
        {
            if(instance != null)
            {
                return instance;
            }

            return LockObjects.GetOrAdd(instance, new object());
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
            lockWasTokenLocal = il.DeclareLocal(typeof(bool));
            tempLocal = il.DeclareLocal(typeof(object));

            il.Emit(method.IsStatic ? Mono.Cecil.Cil.OpCodes.Ldnull : Mono.Cecil.Cil.OpCodes.Ldarg_0);
            il.Emit(Mono.Cecil.Cil.OpCodes.Call, GetLockObjectMethod);
            il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, tempLocal);

            il.BeginExceptionBlock();
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, tempLocal);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloca, lockWasTokenLocal);
            il.Emit(Mono.Cecil.Cil.OpCodes.Call, EnterMethod);
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

            var takenLabel = il.DefineLabel();

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, lockWasTokenLocal);
            il.Emit(Mono.Cecil.Cil.OpCodes.Brfalse, takenLabel);

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, tempLocal);
            il.Emit(Mono.Cecil.Cil.OpCodes.Call, ExitMethod);

            il.MarkLabel(takenLabel);

            il.EndExceptionBlock();
        }
    }
}
