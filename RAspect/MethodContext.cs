namespace RAspect
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Encapsulate method information capture in AOP framework.
    /// </summary>
    public sealed class MethodContext
    {
        /// <summary>
        /// Argument values
        /// </summary>
        private object[] argumentValues;

        /// <summary>
        /// Arguments
        /// </summary>
        private MethodParameterContext[] arguments;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodContext"/> class.
        /// </summary>
        /// <param name="arguments">Arguments</param>
        /// <param name="argumentValues">Argument Values</param>
        public MethodContext(MethodParameterContext[] arguments, object[] argumentValues)
        {
            this.arguments = arguments;
            this.argumentValues = argumentValues;
        }

        /// <summary>
        /// Gets aspect method arguments
        /// </summary>
        public IEnumerable<MethodParameterContext> Arguments
        {
            get
            {
                if (arguments == null)
                {
                    yield break;
                }

                for (var i = 0; i < arguments.Length; i++)
                {
                    var argument = arguments[i];
                    yield return new MethodParameterContext(argument.Name, argument.IsRef) { Value = argumentValues[i] };
                }
            }
        }

        /// <summary>
        /// Gets or sets Method
        /// </summary>
        public MethodInfo Method { get; set; }

        /// <summary>
        /// Gets or sets Method Attributes
        /// </summary>
        public List<Attribute> Attributes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether flag to indicate continuation of code execution
        /// </summary>
        public bool Continue { get; set; } = true;

        /// <summary>
        /// Gets or sets return value of weaved method to be used after execution is completed
        /// </summary>
        public object Returns { get; set; }

        /// <summary>
        /// Gets or sets instance of class weaved method was invoked on
        /// </summary>
        public object Instance { get; set; }

        /// <summary>
        /// Gets or sets token to be used by aspect for passing context between various aspect entry points
        /// </summary>
        public object Token { get; set; }

        /// <summary>
        /// Gets or sets exception for the current method context
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Set values for arguments
        /// </summary>
        /// <param name="values">Values</param>
        public void SetValues(object[] values)
        {
            this.argumentValues = values;
        }

        /// <summary>
        /// Set arguments
        /// </summary>
        /// <param name="arguments">Arguments</param>
        public void SetArguments(MethodParameterContext[] arguments)
        {
            this.arguments = arguments;
        }

        /// <summary>
        /// Get argument value by name from weaved method arguments
        /// </summary>
        /// <typeparam name="T">Generic Type</typeparam>
        /// <param name="name">Argument name</param>
        /// <param name="default">Default value to use in place of missing value</param>
        /// <returns><typeparamref name="T"/></returns>
        public T GetArgument<T>(string name, T @default = default(T))
        {
            var argument = this.Arguments.FirstOrDefault(x => x.Name.Equals(name));
            return argument != null ? (T)argument.Value : @default;
        }
    }
}
