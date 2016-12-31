namespace RAspect
{
    /// <summary>
    /// Represent arguments retrieved from aspect methods
    /// </summary>
    public sealed class MethodParameterContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MethodParameterContext"/> class.
        /// </summary>
        /// <param name="name">Name of argument</param>
        /// <param name="isRef">Flag indicating if argument is reference type</param>
        public MethodParameterContext(string name, bool isRef)
        {
            this.Name = name;
            this.IsRef = isRef;
        }

        /// <summary>
        /// Gets name of aspect method parameter
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets value of aspect method parameter
        /// </summary>
        public object Value { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether method parameter is reference type
        /// </summary>
        public bool IsRef { get; private set; }
    }
}
