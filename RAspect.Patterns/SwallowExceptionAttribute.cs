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
    /// Attribute when applied to method or properties, swallows thrown exception
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Assembly)]
    public sealed class SwallowExceptionAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwallowExceptionAttribute"/> class.
        /// </summary>
        public SwallowExceptionAttribute() : base(WeaveTargetType.Methods | WeaveTargetType.Constructors)
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
                return WeaveBlockType.Inline;
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
            il.BeginExceptionBlock();
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
            il.BeginCatchBlock(typeof(System.Exception));
            il.EndExceptionBlock();
        }
    }
}
