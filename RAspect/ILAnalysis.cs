namespace RAspect
{
    /// <summary>
    /// Analysis of aspect methods IL
    /// </summary>
    public class ILAnalysis
    {
        /// <summary>
        /// Gets or sets whether method property was used
        /// </summary>
        public bool MethodUsed { get; set; }

        /// <summary>
        /// Gets or sets whether arguments property was used
        /// </summary>
        public bool ArgumentsUsed { get; set; }

        /// <summary>
        /// Gets or sets whether continue property was used
        /// </summary>
        public bool ContinueUsed { get; set; }

        /// <summary>
        /// Gets or sets whether instance property was used
        /// </summary>
        public bool InstanceUsed { get; set; }

        /// <summary>
        /// Gets or sets whether return property was used
        /// </summary>
        public bool ReturnUsed { get; set; }

        /// <summary>
        /// Gets or sets whether intercept method is empty
        /// </summary>
        public bool EmptyInterceptMethod { get; set; } = true;

        /// <summary>
        /// Gets or sets whether exit method is empty
        /// </summary>
        public bool EmptyExitMethod { get; set; } = true;

        /// <summary>
        /// Gets or sets whether success method is empty
        /// </summary>
        public bool EmptySuccessMethod { get; set; } = true;

        /// <summary>
        /// Gets or sets whether exception method is empty
        /// </summary>
        public bool EmptyExceptionMethod { get; set; } = true;
    }
}
