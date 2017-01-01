using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Emit;
using System.Reflection;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="NullReferenceException"/> for target it is applied to when they are null, empty or whitespace
    /// </summary>
    public sealed class RequiredAttribute : ContractAspect
    {
        /// <summary>
        /// Invoke before setting property value
        /// </summary>
        /// <param name="context"></param>
        internal override void OnEntry(MethodContext context)
        {
            var argument = context.Arguments.FirstOrDefault();
            if (argument == null)
                return;

            var value = argument.Value;

            if (!value.IsValidateForRequired())
            {
                throw new NullReferenceException("value");
            }
        }

        /// <summary>
        /// Validate value against contract implementation
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="name">Name</param>
        /// <returns>Exception</returns>
        protected override Exception ValidateContract(object value, string name)
        {
            if (!value.IsValidateForRequired())
            {
                return new NullReferenceException(name);
            }
            return null;
        }

        /// <summary>
        /// Aspect code to inject at the beginning of weaved method
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        protected override void BeginAspectBlock(MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
            var requiredLabel = il.DefineLabel();
            var offset = method.IsStatic ? 0 : 1;

            il.Emit(OpCodes.Ldarg, parameter.Position + offset);
            il.Emit(OpCodes.Call, ContractExtensions.GetMethod("IsValidateForRequired").MakeGenericMethod(parameter.ParameterType));
            il.Emit(OpCodes.Brtrue, requiredLabel);

            il.Emit(OpCodes.Ldstr, parameter.Name);
            il.Emit(OpCodes.Newobj, typeof(NullReferenceException).GetConstructor(new[] { typeof(string) }));
            il.Emit(OpCodes.Throw);

            il.MarkLabel(requiredLabel);
        }
    }
}
