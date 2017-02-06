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
    /// Attribute when applied on type, allows static method of type to be substitued with other method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    public class StaticMethodReplacerAttribute : AspectBase
    {
        private Type staticMethodType;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticMethodReplacerAttribute"/> class.
        /// </summary>
        /// <param name="type">Type</param>
        public StaticMethodReplacerAttribute(Type type) : this() {
            this.staticMethodType = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticMethodReplacerAttribute"/> class.
        /// </summary>
        public StaticMethodReplacerAttribute()
        {
            OnBeginAspectBlock = BeginAspectBlock;
            OnAspectMethodCall = AspectMethodCall;
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
            if (!replaceMethod.IsStatic)
                return false;

            var attrs = method.GetCustomAttributes<StaticMethodReplacerAttribute>();
            MethodInfo meth = null;
            foreach (var attr in attrs)
            {
                var staticType = attr.staticMethodType;
                var smeth = staticType.GetMethod(replaceMethod.Name, BindingFlags.Static | BindingFlags.Public);

                if (smeth != null)
                    meth = smeth;
            }

            il.Emit(OpCodes.Call, meth);

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
