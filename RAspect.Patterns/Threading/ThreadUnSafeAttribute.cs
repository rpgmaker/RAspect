using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAspect.Patterns.Exception;
using System.Reflection.Emit;
using System.Reflection;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied to a type, ensures that only one thread executes in methods of this type. When more than one thread accesses methods of this type, a <see cref="ConcurrentAccessException"/> exception is thrown
    /// </summary>
    public sealed class ThreadUnSafeAttribute : AspectBase
    {
        /// <summary>
        /// Field thread safe counter
        /// </summary>
        [ThreadStatic]
        private static Mono.Cecil.FieldDefinition field;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadUnSafeAttribute"/> class.
        /// </summary>
        public ThreadUnSafeAttribute()
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
        /// Aspect code to inject at the beginning of weaved method
        /// </summary>
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void BeginBlock(Mono.Cecil.TypeDefinition typeBuilder, Mono.Cecil.MethodDefinition method, Mono.Cecil.ParameterDefinition parameter, Mono.Cecil.Cil.ILProcessor il)
        {
            field = method.DeclaringType.DefineField("<unsafe>_" + method.Name, typeof(int), 
                Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Private);

            var notZero = il.DefineLabel();

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, field);
            il.Emit(Mono.Cecil.Cil.OpCodes.Brfalse, notZero);

            il.Emit(Mono.Cecil.Cil.OpCodes.Newobj, typeof(ConcurrentAccessException).GetConstructor(Type.EmptyTypes));
            il.Emit(Mono.Cecil.Cil.OpCodes.Throw);

            il.MarkLabel(notZero);

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, field);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4_1);
            il.Emit(Mono.Cecil.Cil.OpCodes.Add);
            il.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, field);
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
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, field);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4_1);
            il.Emit(Mono.Cecil.Cil.OpCodes.Sub);
            il.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, field);
        }
    }
}
