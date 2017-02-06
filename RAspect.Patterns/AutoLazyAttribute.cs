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
        private static Label autoLabel;

        /// <summary>
        /// Field Builder for return value
        /// </summary>
        [ThreadStatic]
        private static FieldBuilder autoField;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoLazyAttribute"/> class.
        /// </summary>
        public AutoLazyAttribute() : base(WeaveTargetType.Properties)
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

            if (returnType == typeof(void) || !(meth.Name.StartsWith("get_") || meth.GetParameters().Length == 0))
                return;

            var isStatic = meth.IsStatic;
            var isPrimitive = returnType.IsPrimitive;

            if (isPrimitive)
            {
                returnType = typeof(Nullable<>).MakeGenericType(returnType);
            }

            autoField = typeBuilder.DefineField("<auto_lazy>_" + meth.Name, returnType,
                isStatic ? FieldAttributes.Static | FieldAttributes.Private : FieldAttributes.Private);
            autoLabel = il.DefineLabel();

            if (!isStatic)
                il.Emit(OpCodes.Ldarg_0);
            il.Emit(isStatic ? OpCodes.Ldsflda : OpCodes.Ldflda, autoField);
            if (isPrimitive)
            {
                il.Emit(OpCodes.Call, autoField.FieldType.GetMethod("get_HasValue"));
            }

            il.Emit(isPrimitive ? OpCodes.Brtrue : OpCodes.Brfalse, autoLabel);
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
            var meth = method as MethodInfo;
            var returnType = meth.ReturnType;

            if (returnType == typeof(void) || !(meth.Name.StartsWith("get_") || meth.GetParameters().Length == 0))
                return;

            var isPrimitive = returnType.IsPrimitive;
            var isStatic = method.IsStatic;
            if (autoField != null)
            {
                var local = il.DeclareLocal(returnType);
                il.Emit(OpCodes.Stloc, local);

                if (!isStatic)
                    il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldloc, local);

                if (isPrimitive)
                {
                    il.Emit(OpCodes.Newobj, typeof(Nullable<>).MakeGenericType(returnType)
                        .GetConstructor(new Type[] { returnType }));
                }

                il.Emit(isStatic ? OpCodes.Stsfld : OpCodes.Stfld, autoField);
            }

            il.MarkLabel(autoLabel);

            if (autoField != null)
            {
                if (!isStatic)
                    il.Emit(OpCodes.Ldarg_0);
                il.Emit(isStatic ? OpCodes.Ldsflda : OpCodes.Ldflda, autoField);
                if (returnType.IsPrimitive) {
                    il.Emit(OpCodes.Call, autoField.FieldType.GetMethod("get_Value"));
                }
            }
        }
    }
}
