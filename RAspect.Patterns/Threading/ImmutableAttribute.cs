using RAspect.Patterns.Exception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied to a class, it implements the immutable threading model. Immutable objects cannot be modified after the constructor exits
    /// </summary>

    public sealed class ImmutableAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableAttribute"/> class.
        /// </summary>
        public ImmutableAttribute()
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
        /// Aspect code to inject at the beginning of weaved method
        /// </summary>
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void BeginBlock(Mono.Cecil.TypeDefinition typeBuilder, Mono.Cecil.MethodDefinition method, Mono.Cecil.ParameterDefinition parameter, Mono.Cecil.Cil.ILProcessor il)
        {
            var methodName = method.Name;
            var isSetProperty = methodName.StartsWith("set_") && !method.IsStatic;

            if (!isSetProperty)
            {
                return;
            }

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, methodName.Substring(4));
            il.Emit(Mono.Cecil.Cil.OpCodes.Newobj, typeof(ObjectReadOnlyException).GetConstructor(new[] { typeof(string) }));
            il.Emit(Mono.Cecil.Cil.OpCodes.Throw);
        }
    }
}
