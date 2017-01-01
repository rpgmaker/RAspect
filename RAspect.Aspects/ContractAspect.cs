using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Assembly)]
    public abstract class ContractAspect : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContractAspect"/> class.
        /// </summary>
        public ContractAspect() : base(WeaveTargetType.Parameters | WeaveTargetType.Properties)
        {
            OnBeginAspectBlock = BeginAspectBlock;
        }

        /// <summary>
        /// Validate value against contract implementation
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="name">Name</param>
        /// <returns>Exception</returns>
        protected abstract Exception ValidateContract(object value, string name);

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
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        protected abstract void BeginAspectBlock(MethodBase method, ParameterInfo parameter, ILGenerator il);
    }
}
