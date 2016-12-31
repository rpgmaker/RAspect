namespace RAspect
{
    using System;

    /// <summary>
    /// Mark attribute for identifying generated Aspect Assemblies.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class RAspectGeneratedAttribute : Attribute
    {
    }
}
