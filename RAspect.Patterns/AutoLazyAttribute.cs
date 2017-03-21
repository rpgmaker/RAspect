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
    /// Attribute when applied to properties/methods causes result to be lazy loaded
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Assembly)]
    public class AutoLazyAttribute : AspectBase
    {
        /// <summary>
        /// Label for defining condition to lazy load
        /// </summary>
        [ThreadStatic]
        private static Mono.Cecil.Cil.Instruction autoLabel;

        /// <summary>
        /// Field for return value
        /// </summary>
        [ThreadStatic]
        private static Mono.Cecil.FieldDefinition autoField;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoLazyAttribute"/> class.
        /// </summary>
        public AutoLazyAttribute() : base(WeaveTargetType.Properties)
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
            var meth = method;
            var module = typeBuilder.Module;
            var returnType = meth.ReturnType.ReflectionType();

            if (returnType == typeof(void) || !(meth.Name.StartsWith("get_") || meth.Parameters.Count == 0))
                return;

            var isStatic = meth.IsStatic;
            var isPrimitive = returnType.IsPrimitive();

            if (isPrimitive)
            {
                returnType = typeof(Nullable<>).MakeGenericType(returnType);
            }

            autoField = method.DeclaringType.DefineField("<auto_lazy>_" + meth.Name, returnType,
                isStatic ? Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Private : Mono.Cecil.FieldAttributes.Private);

            autoLabel = il.DefineLabel();

            if (!isStatic)
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            il.Emit(isStatic ? Mono.Cecil.Cil.OpCodes.Ldsflda : Mono.Cecil.Cil.OpCodes.Ldflda, autoField);
            if (isPrimitive)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Call, module.Import(returnType.GetMethod("get_HasValue")));
            }

            il.Emit(isPrimitive ? Mono.Cecil.Cil.OpCodes.Brtrue : Mono.Cecil.Cil.OpCodes.Brfalse, autoLabel);
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
            var meth = method;
            var module = typeBuilder.Module;
            var returnType = meth.ReturnType.ReflectionType();

            if (returnType == typeof(void) || !(meth.Name.StartsWith("get_") || meth.Parameters.Count == 0))
                return;

            var isPrimitive = returnType.IsPrimitive();
            var isStatic = method.IsStatic;
            if (autoField != null)
            {
                //var local = il.DeclareLocal(returnType);

                //il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, 0);
                //il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, local);

                if (!isStatic)
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, 0);

                if (isPrimitive)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Newobj, typeof(Nullable<>).MakeGenericType(returnType)
                        .GetConstructor(new Type[] { returnType }));
                }

                il.Emit(isStatic ? Mono.Cecil.Cil.OpCodes.Stsfld : Mono.Cecil.Cil.OpCodes.Stfld, autoField);
            }

            il.MarkLabel(autoLabel);

            if (autoField != null)
            {
                if (!isStatic)
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                il.Emit(isStatic ? Mono.Cecil.Cil.OpCodes.Ldsflda : Mono.Cecil.Cil.OpCodes.Ldflda, autoField);
                if (isPrimitive) {
                    returnType = typeof(Nullable<>).MakeGenericType(returnType);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Call, module.Import(returnType.GetMethod("get_Value")));
                }
                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, 0);
            }
        }
    }
}
