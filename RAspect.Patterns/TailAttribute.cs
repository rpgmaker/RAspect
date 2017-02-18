using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns
{
    /// <summary>
    /// Attribute when applied on type/methods will include Tail call instruction for recursive functions
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, Inherited = true)]
    public class TailAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TailAttribute"/> class.
        /// </summary>
        public TailAttribute()
        {
            OnAspectMethodCall = AspectMethodCall;
            OnBeginAspectBlock = BeginAspectBlock;
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
        /// Aspect method to use for method call substition
        /// </summary>
        /// <param name="typeBuilder"></param>
        /// <param name="il"></param>
        /// <param name="method"></param>
        /// <param name="replaceMethod">Replace Method</param>
        /// <returns></returns>
        internal bool AspectMethodCall(TypeBuilder typeBuilder, ILGenerator il, MethodBase method, MethodBase replaceMethod)
        {
            var isRecursive = method == replaceMethod;
            var declaringType = replaceMethod.DeclaringType;
            var methodCall = replaceMethod as MethodInfo;
            if (isRecursive)
            {
                methodCall = typeBuilder.GetMethodEx(methodCall.Name + "_", methodCall.ReturnType, methodCall.GetParameters().Select(x => x.ParameterType).ToArray());
                il.Emit(OpCodes.Tailcall);
            }

            il.Emit(declaringType.IsValueType || replaceMethod.IsStatic || methodCall.ReturnType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, 
                methodCall);

            il.Emit(OpCodes.Ret);

            return true;
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
        }
    }
}
