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
    public sealed class TailAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TailAttribute"/> class.
        /// </summary>
        public TailAttribute()
        {
            OnMethodCall = MethodCall;
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
        /// Aspect method to use for method call substition
        /// </summary>
        /// <param name="typeBuilder"></param>
        /// <param name="il"></param>
        /// <param name="method"></param>
        /// <param name="replaceMethod">Replace Method</param>
        /// <returns></returns>
        internal bool MethodCall(Mono.Cecil.TypeDefinition typeBuilder, Mono.Cecil.Cil.ILProcessor il, Mono.Cecil.MethodDefinition method, Mono.Cecil.MethodDefinition replaceMethod)
        {
            var methodName = method.Name.Replace("~", string.Empty);
            var isRecursive = methodName == replaceMethod.Name;
            var declaringType = replaceMethod.DeclaringType;
            var methodCall = replaceMethod;
            if (isRecursive)
            {
                methodCall = typeBuilder.GetMethodEx(methodCall.Name, methodCall.ReturnType, methodCall.Parameters.Select(x => x.ParameterType).ToArray());
                il.Emit(Mono.Cecil.Cil.OpCodes.Tail);
            }

            il.Emit(declaringType.IsValueType || replaceMethod.IsStatic || methodCall.ReturnType.IsValueType ? Mono.Cecil.Cil.OpCodes.Call : Mono.Cecil.Cil.OpCodes.Callvirt, 
                methodCall);

            il.Emit(Mono.Cecil.Cil.OpCodes.Ret);

            return true;
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
        }
    }
}
