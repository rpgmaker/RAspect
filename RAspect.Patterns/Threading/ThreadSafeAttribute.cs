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
    public class ThreadSafeAttribute : AspectBase
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
        /// Local Builder for return value
        /// </summary>
        [ThreadStatic]
        private static LocalBuilder exLocal;

        /// <summary>
        /// Local Builder for monitor.enter
        /// </summary>
        [ThreadStatic]
        private static LocalBuilder lockWasTokenLocal;

        /// <summary>
        /// Local Builder for monitor.enter
        /// </summary>
        [ThreadStatic]
        private static LocalBuilder tempLocal;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadSafeAttribute"/> class.
        /// </summary>
        public ThreadSafeAttribute()
        {
            OnBeginAspectBlock = BeginAspectBlock;
            OnEndAspectBlock = EndAspectBlock;
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
        internal static object GetLockObject(object instance)
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
        internal void BeginAspectBlock(TypeBuilder typeBuilder, MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
            var meth = method as MethodInfo;
            var returnType = meth.ReturnType;
            exLocal = returnType != typeof(void) ? il.DeclareLocal(returnType) : null;
            lockWasTokenLocal = il.DeclareLocal(typeof(bool));
            tempLocal = il.DeclareLocal(typeof(object));

            il.Emit(method.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, GetLockObjectMethod);
            il.Emit(OpCodes.Stloc, tempLocal);

            il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldloc, tempLocal);
            il.Emit(OpCodes.Ldloca, lockWasTokenLocal);
            il.Emit(OpCodes.Call, EnterMethod);
        }

        /// <summary>
        /// Aspect code to inject at the end of weaved method
        /// </summary>
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void EndAspectBlock(TypeBuilder typeBuilder, MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
            if (exLocal != null)
                il.Emit(OpCodes.Stloc, exLocal);

            il.BeginFinallyBlock();

            var takenLabel = il.DefineLabel();

            il.Emit(OpCodes.Ldloc, lockWasTokenLocal);
            il.Emit(OpCodes.Brfalse, takenLabel);

            il.Emit(OpCodes.Ldloc, tempLocal);
            il.Emit(OpCodes.Call, ExitMethod);

            il.MarkLabel(takenLabel);

            il.EndExceptionBlock();

            if (exLocal != null)
                il.Emit(OpCodes.Ldloc, exLocal);
        }
    }
}
