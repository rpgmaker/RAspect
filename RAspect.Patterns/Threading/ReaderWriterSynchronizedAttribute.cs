using RAspect.Patterns.Exception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied to a class will require at ReaderAttribute and WriterAttribute to be applied to method or properties otherwise it will throw exception
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
    public sealed class ReaderWriterSynchronizedAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderWriterSynchronizedAttribute"/> class.
        /// </summary>
        public ReaderWriterSynchronizedAttribute() : base(WeaveTargetType.Class)
        {
        }

        /// <summary>
        /// Validate if Reader and Writer are properly applied to given type
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="methods">Type Methods</param>
        internal override void ValidateRules(Mono.Cecil.TypeDefinition type, IEnumerable<Mono.Cecil.MethodDefinition> methods)
        {
            var hasReader = methods.Any(x => x.GetCustomAttribute<ReaderAttribute>() != null);
            var hasWriter = methods.Any(x => x.GetCustomAttribute<WriterAttribute>() != null);
            
            if (!(hasReader && hasWriter))
            {
                throw new ThreadingValidationException(String.Format("ReaderWriterSynchronize validation: {0} must have at least one reader and writer attribute for it members", type.FullName));
            }
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
    }
}
