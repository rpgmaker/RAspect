using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Text.RegularExpressions;
using System.IO;
using System.Runtime.CompilerServices;

namespace RAspect
{
    /// <summary>
    /// IL Weaver for weaving method/types with various functionalities
    /// </summary>
    public static class ILWeaver
    {
        /// <summary>
        /// Generated assembly name
        /// </summary>
        internal const string ASM_NAME = "RAspect_ILWeaving";

        /// <summary>
        /// Lock for generating dynamic assembly
        /// </summary>
        private static readonly object LockObject = new object();

        /// <summary>
        /// Binding for non public
        /// </summary>
        internal static readonly BindingFlags NonPublicBinding = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Dictionary for keeping track of generated weaved types
        /// </summary>
        private static readonly ConcurrentDictionary<string, Type> Types = new ConcurrentDictionary<string, Type>();

        /// <summary>
        /// Dictionary for keeping track of cil analysis
        /// </summary>
        private static readonly ConcurrentDictionary<Type, ILAnalysis> AspectAnalysises = new ConcurrentDictionary<Type, ILAnalysis>();

        /// <summary>
        /// Dictionary for keeping track of defined methods
        /// </summary>
        internal static readonly ConcurrentDictionary<string, MethodBuilder> DefinedMethods = new ConcurrentDictionary<string, MethodBuilder>();

        /// <summary>
        /// Dictionary for keeping track of fields for enter/exit/error/etc
        /// </summary>
        internal static readonly ConcurrentDictionary<string, Dictionary<string, FieldBuilder>> TypeAspects = new ConcurrentDictionary<string, Dictionary<string, FieldBuilder>>();

        /// <summary>
        /// Dictionary for keeping track of weaved type flag
        /// </summary>
        private static readonly ConcurrentDictionary<Type, bool> TypeAspectFlags = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// Aspect type
        /// </summary>
        private static readonly Type AspectType = typeof(AspectBase);

        /// <summary>
        /// Method Context type
        /// </summary>
        private static readonly Type MethodContextType = typeof(MethodContext);

        /// <summary>
        /// To List method info
        /// </summary>
        private static readonly MethodInfo ToListAttributeMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(typeof(Attribute));

        /// <summary>
        /// Get Custom Attributes for method info
        /// </summary>
        private static readonly MethodInfo GetMethodAttributesMethod = typeof(ILWeaver).GetMethod("GetMethodAttributes", NonPublicBinding);
        /// <summary>
        /// Aspect Entry method info
        /// </summary>
        private static readonly MethodInfo AspectEntry = AspectType.GetMethod("OnEntry", NonPublicBinding);

        /// <summary>
        /// GetDefaultValue method info
        /// </summary>
        private static readonly MethodInfo GetDefaultValueMethod = typeof(ILWeaver).GetMethod("GetDefaultValue", BindingFlags.Static | BindingFlags.Public);

        /// <summary>
        /// Aspect Exit method info
        /// </summary>
        private static readonly MethodInfo AspectExit = AspectType.GetMethod("OnExit", NonPublicBinding);

        /// <summary>
        /// Aspect Exit method info
        /// </summary>
        private static readonly MethodInfo AspectSuccess = AspectType.GetMethod("OnSuccess", NonPublicBinding);

        /// <summary>
        /// Aspect Error method info
        /// </summary>
        private static readonly MethodInfo AspectException = AspectType.GetMethod("OnException", NonPublicBinding);

        /// <summary>
        /// Get MethodContext
        /// </summary>
        private static readonly MethodInfo GetAspectValue = typeof(ILWeaver).GetMethod("GetAspect");

        /// <summary>
        /// MethodContext Returns Property
        /// </summary>
        private static readonly PropertyInfo MethodContextReturns = MethodContextType.GetProperty("Returns");

        /// <summary>
        /// MethodContext Arguments Property
        /// </summary>
        private static readonly PropertyInfo MethodContextArguments = MethodContextType.GetProperty("Arguments");

        /// <summary>
        /// MethodContext Continue Property
        /// </summary>
        private static readonly PropertyInfo MethodContextContinue = MethodContextType.GetProperty("Continue");

        /// <summary>
        /// MethodContext Method Property
        /// </summary>
        private static readonly PropertyInfo MethodContextMethod = MethodContextType.GetProperty("Method");

        /// <summary>
        /// MethodContext Method Property
        /// </summary>
        private static readonly PropertyInfo MethodContextAttributes = MethodContextType.GetProperty("Attributes");

        /// <summary>
        /// MethodContext Instance Property
        /// </summary>
        private static readonly PropertyInfo MethodContextInstance = MethodContextType.GetProperty("Instance");

        /// <summary>
        /// Default constructor for object class
        /// </summary>
        private static readonly ConstructorInfo ObjectCtor = typeof(object).GetConstructor(Type.EmptyTypes);

        /// <summary>
        /// Collection of cached aspect instances
        /// </summary>
        private static readonly ConcurrentDictionary<Type, AspectBase> Aspects = new ConcurrentDictionary<Type, AspectBase>();

        /// <summary>
        /// Constructor info for MethodContext ArgumentInfo
        /// </summary>
        private static ConstructorInfo MethodParameterContextCtor = typeof(MethodParameterContext).GetConstructor(new[] { typeof(string), typeof(bool) });

        /// <summary>
        /// AssemblyBuilder for aspect types
        /// </summary>
        private static AssemblyBuilder asmBuilder;

        /// <summary>
        /// ModuleBuilder for aspect dynamic assembly
        /// </summary>
        private static ModuleBuilder moduleBuilder;

        /// <summary>
        /// Prefix counter for fields/method definitions
        /// </summary>
        private static long counter = 0;

        /// <summary>
        /// Initializes static members of the <see cref="ILWeaver"/> class.
        /// </summary>
        static ILWeaver()
        {
            var debug = false;
            asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(ASM_NAME) { Version = new Version(1, 0, 0, 0) }, AssemblyBuilderAccess.RunAndSave);
            asmBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes), new object[] { }));
#if DEBUG
            var debugCtor = typeof(DebuggableAttribute).GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
            asmBuilder.SetCustomAttribute(new CustomAttributeBuilder(debugCtor, new object[] { DebuggableAttribute.DebuggingModes.Default |
                DebuggableAttribute.DebuggingModes.DisableOptimizations }));
            debug = true;
#endif
            var filename = string.Concat(ASM_NAME, ".dll");
            moduleBuilder = asmBuilder.DefineDynamicModule(filename, filename, debug);
        }

        /// <summary>
        /// Rewrite weaved methods for given generic type
        /// </summary>
        /// <typeparam name="T">Generic Type</typeparam>
        public static void Weave<T>()
            where T : class
        {
            Weave(typeof(T));
        }

        /// <summary>
        /// Rewrite methods for given type
        /// </summary>
        /// <param name="type">Class Type</param>
        public static void Weave(Type type)
        {
            try
            {
                var hasAspect = HasAspect(type);
                
                if (!hasAspect)
                {
                    return;
                }

                lock (LockObject)
                {
                    Type weaveType = null;
                    var key = type.FullName;

                    if (!Types.TryGetValue(key, out weaveType))
                    {
                        weaveType = WeaveType(type);

                        //Trigger static constructor
                        Activator.CreateInstance(weaveType);

                        Types.GetOrAdd(key, weaveType);
                    }
                }
            }
            catch (Exception ex) when (!ex.GetType().FullName.StartsWith("RAspect."))
            {
                throw new ApplicationException(string.Format("Error weaving methods for {0}", type.FullName), ex);
            }
        }

        /// <summary>
        /// Get aspect instance from cache
        /// </summary>
        /// <typeparam name="T">Generic Type</typeparam>
        /// <returns><typeparamref name="T"/></returns>
        public static T GetAspect<T>()
            where T : AspectBase
        {
            var type = typeof(T);
            AspectBase aspect = null;
            if (!Aspects.TryGetValue(type, out aspect))
            {
                Aspects[type] = aspect = Activator.CreateInstance<T>() as AspectBase;
            }

            return (T)aspect;
        }

        /// <summary>
        /// Return default value for given generic type
        /// </summary>
        /// <typeparam name="T">Generic Type</typeparam>
        /// <returns>T</returns>
        public static T GetDefaultValue<T>()
        {
            return default(T);
        }

        /// <summary>
        /// Weave all methods for current assembly
        /// </summary>
        public static void Weave()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var aspectTypes = new List<Type>();

            foreach (var asm in assemblies)
            {
                try
                {
                    var asmName = asm.GetName().Name;
                    if (asmName.StartsWith("System") || asmName.StartsWith("Microsoft") || asmName.StartsWith("mscorlib"))
                    {
                        continue;
                    }

                    var attrs = asm.GetCustomAttributes<AspectBase>();
                    var asmAspects = attrs.Any() ? attrs : null;

                    foreach (var type in asm.GetTypes())
                    {
                        if (HasAspect(type, asmAspects))
                        {
                            aspectTypes.Add(type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var types = ex.Types != null ? ex.Types : null;
                    if (types != null)
                    {
                        foreach (var type in types)
                        {
                            if(type == null)
                            {
                                continue;
                            }

                            var attrs = type.Assembly.GetCustomAttributes<AspectBase>();
                            var asmAspects = attrs.Any() ? attrs : null;

                            if (HasAspect(type, asmAspects))
                            {
                                aspectTypes.Add(type);
                            }
                        }
                    }
                }
            }

            foreach (var aspectType in aspectTypes)
            {
                Weave(aspectType);
            }
        }

        /// <summary>
        /// Save Assembly to disk
        /// </summary>
        /// <param name="fileName">Optional Filename</param>
        public static void SaveAssembly()
        {
            var asmFileName = string.Concat(asmBuilder.GetName().Name, ".dll");
            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, asmFileName);

            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
            catch { }

            asmBuilder.Save(asmFileName);
        }

        /// <summary>
        /// Determine if the given type has aspect
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="asmAspects">Assembly</param>
        /// <returns>Bool</returns>
        private static bool HasAspect(Type type, IEnumerable<AspectBase> asmAspects = null)
        {
            bool success = false;

            if (type == null || type.Name.Contains("Anonymous"))
            {
                return false;
            }

            if (TypeAspectFlags.TryGetValue(type, out success))
            {
                return success;
            }

            var typeAspects = GetValidAspects(type, asmAspects);

            if(typeAspects.Any() && typeAspects.All(x => x.Exclude))
            {
                return TypeAspectFlags[type] = false;
            }

            var methods = type.GetMethods(NonPublicBinding).Where(x => x.DeclaringType == type);

            var props = type.GetProperties(NonPublicBinding).Where(x => x.DeclaringType == type);

            var fields = type.GetFields(NonPublicBinding).Where(x => x.DeclaringType == type);

            foreach (var method in methods)
            {
                typeAspects.AddRange(method.GetCustomAttributes<AspectBase>().Where(x => !x.Exclude));
            }

            foreach (var prop in props)
            {
                typeAspects.AddRange(prop.GetCustomAttributes<AspectBase>().Where(x => !x.Exclude));
            }

            foreach (var field in fields)
            {
                typeAspects.AddRange(field.GetCustomAttributes<AspectBase>().Where(x => !x.Exclude));
            }

            return success = TypeAspectFlags[type] = typeAspects.Any();
        }

        /// <summary>
        /// Get applicable aspect for the given type and include assembly aspect if available
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="asmAspects">Optional Assembly Aspects</param>
        /// <returns></returns>
        private static List<AspectBase> GetValidAspects(Type type, IEnumerable<AspectBase> asmAspects = null)
        {
            var aspectAttributes = type.GetCustomAttributes<AspectBase>().Where(x => !x.Exclude).ToList();

            var fullName = type.FullName;

            var assemblyAspects = (asmAspects ?? type.Assembly.GetCustomAttributes<AspectBase>()).Where(x => !x.Exclude);

            foreach (var aspect in assemblyAspects)
            {
                var searchPattern = aspect.SearchTypePattern;
                var isValid = aspect.Target != 0 && (string.IsNullOrWhiteSpace(searchPattern) ||
                    Regex.IsMatch(fullName, searchPattern));

                if (!isValid)
                    continue;

                aspectAttributes.Add(aspect);
            }

            return aspectAttributes;
        }

        /// <summary>
        /// Get cached aspect information (for improvement of generated code) regarding weaved methods
        /// </summary>
        /// <param name="aspectType">Aspect Type</param>
        /// <returns>ILAnalysis</returns>
        private static ILAnalysis GetCachedILAnalysis(Type aspectType)
        {
            lock (LockObject)
                return AspectAnalysises.GetOrAdd(aspectType, _ => GetILAnalysis(aspectType));
        }

        /// <summary>
        /// Get aspect information regarding entry/exit/exception
        /// </summary>
        /// <param name="aspectType">Aspect Type</param>
        /// <returns>ILAnalysis</returns>
        private static ILAnalysis GetILAnalysis(Type aspectType)
        {
            var methods = new[] {
                aspectType.GetMethod("OnEntry", NonPublicBinding),
                aspectType.GetMethod("OnExit", NonPublicBinding),
                aspectType.GetMethod("OnException", NonPublicBinding),
                aspectType.GetMethod("OnSuccess", NonPublicBinding),
                aspectType.GetMethod("OnEnter", NonPublicBinding),
                aspectType.GetMethod("OnLeave", NonPublicBinding),
                aspectType.GetMethod("OnError", NonPublicBinding),
                aspectType.GetMethod("OnComplete", NonPublicBinding)
            };

            var analysis = new ILAnalysis();
            var @continue = MethodContextContinue.GetSetMethod();
            var @continueGet = MethodContextContinue.GetGetMethod();
            var arguments = MethodContextArguments.GetGetMethod();
            var instance = MethodContextInstance.GetGetMethod();
            var meth = MethodContextMethod.GetGetMethod();
            var @return = MethodContextReturns.GetGetMethod();
            var @break = false;

            foreach (var method in methods)
            {
                if (method == null)
                {
                    continue;
                }

                var methodName = method.Name;
                var cilReader = new ILReader(method);
                while (cilReader.Read())
                {
                    var current = cilReader.Current;

                    if (current.Instruction.Value == OpCodes.Nop.Value)
                    {
                        continue;
                    }

                    var propMethod = current.Data as MethodInfo;
                    
                    if (current.Instruction.Value != OpCodes.Ret.Value)
                    {
                        if (methodName == "OnEntry" || methodName == "OnEnter")
                        {
                            analysis.EmptyInterceptMethod = false;
                        }

                        if (methodName == "OnExit" || methodName == "OnLeave")
                        {
                            analysis.EmptyExitMethod = false;
                        }

                        if (methodName == "OnException" || methodName == "OnError")
                        {
                            analysis.EmptyExceptionMethod = false;
                        }

                        if (methodName == "OnSuccess" || methodName == "OnComplete")
                        {
                            analysis.EmptySuccessMethod = false;
                        }
                    }

                    if (propMethod == null)
                    {
                        continue;
                    }

                    analysis.ContinueUsed = analysis.ContinueUsed || (@continue == propMethod || @continueGet == propMethod);
                    analysis.ArgumentsUsed = analysis.ArgumentsUsed || arguments == propMethod;
                    analysis.InstanceUsed = analysis.InstanceUsed || instance == propMethod;
                    analysis.MethodUsed = analysis.MethodUsed || meth == propMethod;
                    analysis.ReturnUsed = analysis.ReturnUsed || @return == propMethod;

                    if (analysis.ContinueUsed && analysis.ArgumentsUsed && analysis.InstanceUsed && analysis.MethodUsed && analysis.ReturnUsed)
                    {
                        @break = true;
                        break;
                    }
                }

                if (@break)
                {
                    break;
                }
            }

            return analysis;
        }

        /// <summary>
        /// Weave method from method info
        /// </summary>
        /// <param name="type">TypeBuilder</param>
        /// <param name="method">Method Info</param>
        /// <param name="aspectAttributes">Collection of aspects</param>
        /// <param name="il">IL Generator</param>
        /// <param name="local">Return local builder</param>
        /// <param name="methodInfoField">MethodInfo Field</param>
        /// <param name="methodAttrField">Method Attribute Field</param>
        /// <param name="sil">Static Constructor IL Generator</param>
        /// <param name="aspectTypes">Aspect Types</param>
        /// <param name="parameterAspects">Parameters Aspect</param>
        /// <param name="methodParameters">Method Parameters</param>
        /// <param name="fieldAspects">Field Aspects</param>
        private static void WeaveMethod(TypeBuilder type, MethodBase method, List<AspectBase> aspectAttributes, ILGenerator il, LocalBuilder local, FieldBuilder methodInfoField, FieldBuilder methodAttrField, ILGenerator sil, List<Type> aspectTypes, List<AspectBase> parameterAspects, Type[] methodParameters, List<AspectBase> fieldAspects)
        {
            var argumentsField = type.DefineField(string.Concat("_<args>_", method.Name, counter++), typeof(MethodParameterContext[]), FieldAttributes.Static | FieldAttributes.Private);
            var fields = TypeAspects.GetOrAdd(type.FullName, _ => new Dictionary<string, FieldBuilder>());
            
            foreach (var aspectType in aspectTypes.Union(fieldAspects.Select(x => x.GetType())).Union(parameterAspects.Select(x => x.GetType())))
            {
                var key = aspectType.FullName;
                FieldBuilder staticField = null;

                if (!fields.TryGetValue(key, out staticField))
                {
                    fields[key] = staticField = type.DefineField(string.Concat("_<aspect>", key), aspectType, FieldAttributes.Private | FieldAttributes.Static);
                    sil.Emit(OpCodes.Call, GetAspectValue.MakeGenericMethod(aspectType));
                    sil.Emit(OpCodes.Stsfld, staticField);
                }
            }

            var isStatic = method.IsStatic;
            var parameterOffset = isStatic ? 0 : 1;

            var analysises = aspectTypes.Select(x => GetCachedILAnalysis(x)).ToList();
            var continueUsed = analysises.Any(x => x.ContinueUsed);
            var methodUsed = analysises.Any(x => x.MethodUsed);
            var argumentsUsed = analysises.Any(x => x.ArgumentsUsed);
            var instanceUsed = analysises.Any(x => x.InstanceUsed);
            var returnUsed = analysises.Any(x => x.ReturnUsed);
            var usesNone = !(continueUsed || methodUsed || argumentsUsed || instanceUsed || returnUsed);
            var needTryCatch = analysises.Any(x => !x.EmptyExceptionMethod || !x.EmptyExitMethod);

            var parameters = method.GetParameters();
            var count = parameters.Where(x => !x.IsOut).Count();

            var methodContext = usesNone ? null : il.DeclareLocal(typeof(MethodContext));
            var argumentValues = count > 0 && argumentsUsed ? il.DeclareLocal(typeof(object[])) : null;

            var continueLabel = continueUsed ? il.DefineLabel() : default(Label);
            var notContinueLabel = continueUsed ? il.DefineLabel() : default(Label);
            var continueLocal = continueUsed ? il.DeclareLocal(typeof(bool)) : null;
            var ex = il.DeclareLocal(typeof(Exception));

            var index = 0;

            if (argumentValues != null)
            {
                sil.Emit(OpCodes.Ldc_I4, count);
                sil.Emit(OpCodes.Newarr, typeof(MethodParameterContext));
                sil.Emit(OpCodes.Stsfld, argumentsField);

                il.Emit(OpCodes.Ldc_I4, count);
                il.Emit(OpCodes.Newarr, typeof(object));
                il.Emit(OpCodes.Stloc, argumentValues);
            }

            for (var i = 0; argumentsUsed && i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.IsOut)
                {
                    continue;
                }

                sil.Emit(OpCodes.Ldsfld, argumentsField);
                sil.Emit(OpCodes.Ldc_I4, index);
                sil.Emit(OpCodes.Ldstr, parameter.Name);

                sil.Emit(OpCodes.Ldc_I4, parameter.ParameterType.IsByRef ? 1 : 0);
                sil.Emit(OpCodes.Newobj, MethodParameterContextCtor);
                sil.Emit(OpCodes.Stelem_Ref);

                il.Emit(OpCodes.Ldloc, argumentValues);
                il.Emit(OpCodes.Ldc_I4, index);
                il.Emit(OpCodes.Ldarg, i + parameterOffset);
                if (parameter.ParameterType.IsValueType || parameter.ParameterType.IsGenericParameter)
                {
                    il.Emit(OpCodes.Box, parameter.ParameterType);
                }

                il.Emit(OpCodes.Stelem_Ref);

                index++;
            }

            if (methodContext != null)
            {
                if (argumentValues != null)
                {
                    il.Emit(OpCodes.Ldsfld, argumentsField);
                    il.Emit(OpCodes.Ldloc, argumentValues);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldnull);
                }

                il.Emit(OpCodes.Newobj, methodContext.LocalType.GetConstructors()[0]);
                il.Emit(OpCodes.Stloc, methodContext);
            }

            if (!isStatic && (instanceUsed && methodContext != null))
            {
                il.Emit(OpCodes.Ldloc, methodContext);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, MethodContextInstance.GetSetMethod());
            }

            if (methodUsed)
            {
                il.Emit(OpCodes.Ldloc, methodContext);
                il.Emit(OpCodes.Ldsfld, methodInfoField);
                il.Emit(OpCodes.Callvirt, MethodContextMethod.GetSetMethod());

                var needAttrs = aspectAttributes.Any(x => x.BlockType == WeaveBlockType.Inline) ||
                    fieldAspects.Any(x => x.BlockType == WeaveBlockType.Inline) ||
                    parameterAspects.Any(x => x.BlockType == WeaveBlockType.Inline);

                if (needAttrs)
                {
                    il.Emit(OpCodes.Ldloc, methodContext);
                    il.Emit(OpCodes.Ldsfld, methodAttrField);
                    il.Emit(OpCodes.Callvirt, MethodContextAttributes.GetSetMethod());
                }
            }

            for (var i = 0; i < aspectTypes.Count; i++)
            {
                var aspectType = aspectTypes[i];
                var analysis = analysises[i];

                if (analysis.EmptyInterceptMethod)
                {
                    continue;
                }

                il.Emit(OpCodes.Ldsfld, fields[aspectType.FullName]);
                if (methodContext != null)
                {
                    il.Emit(OpCodes.Ldloc, methodContext);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                il.Emit(OpCodes.Callvirt, AspectEntry);
            }

            if (continueUsed)
            {
                il.Emit(OpCodes.Ldloc, methodContext);
                il.Emit(OpCodes.Callvirt, MethodContextContinue.GetGetMethod());
                il.Emit(OpCodes.Stloc, continueLocal);

                il.Emit(OpCodes.Ldloc, continueLocal);
                il.Emit(OpCodes.Brfalse, continueLabel);
            }

            if (needTryCatch)
                il.BeginExceptionBlock();

            var methodReturnType = method.IsConstructor ? typeof(void) : (method as MethodInfo).ReturnType;
            var clonedMethod = GenerateReplacementMethod(type, method, method.Attributes, methodReturnType, methodParameters, sil, aspectAttributes.Union(fieldAspects).ToList(), parameterAspects, methodContext);

            InvokeClonedMethod(il, local, clonedMethod, isStatic, parameterOffset, parameters);

            if (local != null)
            {
                if (returnUsed && methodContext != null)
                {
                    il.Emit(OpCodes.Ldloc, methodContext);
                    il.Emit(OpCodes.Ldloc, local);
                    if (methodReturnType.IsValueType || methodReturnType.IsGenericParameter)
                    {
                        il.Emit(OpCodes.Box, methodReturnType);
                    }

                    il.Emit(OpCodes.Callvirt, MethodContextReturns.GetSetMethod());
                }
            }

            for (var i = 0; i < aspectTypes.Count; i++)
            {
                var aspectType = aspectTypes[i];
                var analysis = analysises[i];

                if (analysis.EmptySuccessMethod)
                {
                    continue;
                }

                il.Emit(OpCodes.Ldsfld, fields[aspectType.FullName]);
                if (methodContext != null)
                {
                    il.Emit(OpCodes.Ldloc, methodContext);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                il.Emit(OpCodes.Callvirt, AspectSuccess);
            }

            if (local != null)
            {
                if (returnUsed && methodContext != null)
                {
                    var aspectNoReturnLabel = il.DefineLabel();
                    var localType = local.LocalType;

                    il.Emit(OpCodes.Ldloc, methodContext);
                    il.Emit(OpCodes.Callvirt, MethodContextReturns.GetGetMethod());
                    il.Emit(OpCodes.Brfalse, aspectNoReturnLabel);

                    il.Emit(OpCodes.Ldloc, methodContext);
                    il.Emit(OpCodes.Callvirt, MethodContextReturns.GetGetMethod());
                    if (localType.IsValueType)
                        il.Emit(OpCodes.Unbox_Any, localType);
                    else
                        il.Emit(OpCodes.Isinst, localType); 
                    
                    il.Emit(OpCodes.Stloc, local);

                    il.MarkLabel(aspectNoReturnLabel);
                }
            }

            if (needTryCatch)
            {
                il.BeginCatchBlock(ex.LocalType);

                il.Emit(OpCodes.Stloc, ex);
            }

            for (var i = 0; needTryCatch && i < aspectTypes.Count; i++)
            {
                var aspectType = aspectTypes[i];
                var analysis = analysises[i];

                if (analysis.EmptyExceptionMethod)
                {
                    continue;
                }

                il.Emit(OpCodes.Ldsfld, fields[aspectType.FullName]);
                if (methodContext != null)
                {
                    il.Emit(OpCodes.Ldloc, methodContext);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                il.Emit(OpCodes.Ldloc, ex);
                il.Emit(OpCodes.Callvirt, AspectException);
            }

            if (needTryCatch)
            {
                il.Emit(OpCodes.Rethrow);

                il.BeginFinallyBlock();

                for (var i = 0; i < aspectTypes.Count; i++)
                {
                    var aspectType = aspectTypes[i];
                    var analysis = analysises[i];

                    if (analysis.EmptyExitMethod)
                    {
                        continue;
                    }

                    il.Emit(OpCodes.Ldsfld, fields[aspectType.FullName]);
                    if (methodContext != null)
                    {
                        il.Emit(OpCodes.Ldloc, methodContext);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldnull);
                    }

                    il.Emit(OpCodes.Callvirt, AspectExit);
                }

                il.EndExceptionBlock();
            }

            if (continueUsed)
            {
                il.Emit(OpCodes.Br, notContinueLabel);

                il.MarkLabel(continueLabel);

                if (local != null)
                {
                    var localType = local.LocalType;
                    if (localType.IsValueType)
                    {
                        il.Emit(OpCodes.Ldloca, local);
                        il.Emit(OpCodes.Initobj, localType);
                    }
                    else
                    {
                        if (localType.IsGenericParameter)
                        {
                            il.Emit(OpCodes.Call, GetDefaultValueMethod.MakeGenericMethod(localType));
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldnull);
                        }

                        il.Emit(OpCodes.Stloc, local);
                    }
                }

                il.MarkLabel(notContinueLabel);
            }
        }

        /// <summary>
        /// Invoke original cloned method
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="local">Local for return type methods</param>
        /// <param name="clonedMethod">Cloned Method</param>
        /// <param name="isStatic">Is Static</param>
        /// <param name="parameterOffset">Parameter Offses</param>
        /// <param name="parameters">Parameters</param>
        private static void InvokeClonedMethod(ILGenerator il, LocalBuilder local, MethodBuilder clonedMethod, bool isStatic, int parameterOffset, ParameterInfo[] parameters)
        {
            if (!isStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i + parameterOffset);
            }

            il.Emit(OpCodes.Callvirt, clonedMethod);

            if (local != null)
            {
                il.Emit(OpCodes.Stloc, local);
            }
        }

        /// <summary>
        /// Return true if system defined
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Bool</returns>
        private static bool IsSystemDefined(this Type type)
        {
            return type.Namespace.StartsWith("system", StringComparison.OrdinalIgnoreCase) ||
                type.Module.ScopeName == "CommonLanguageRuntimeLibrary";
        }

        /// <summary>
        /// Get method/properties attributes for a given method
        /// </summary>
        /// <param name="method"></param>
        /// <returns>List{Attribute}</returns>
        internal static List<Attribute> GetMethodAttributes(MethodBase method)
        {
            var declaringType = method.DeclaringType;

            var methodName = method.Name;

            var isProperty = methodName.StartsWith("get_") || methodName.StartsWith("set_");

            var newMethodName = isProperty ? methodName.Substring(4) : methodName;

            var propInfo = isProperty ? declaringType.GetProperty(newMethodName, NonPublicBinding) : null;

            return (isProperty ? propInfo.GetCustomAttributes<Attribute>() :
                method.GetCustomAttributes<Attribute>()).ToList();
        }

        /// <summary>
        /// Determine if aspect should be applied to the given field
        /// </summary>
        /// <param name="field">Field</param>
        /// <param name="aspect">Aspect</param>
        /// <returns></returns>
        internal static bool IsValidAspectFor(FieldInfo field, AspectBase aspect, bool allowEvents = false)
        {
            var fieldName = field.Name;

            var eventField = field.DeclaringType.GetEvent(fieldName, NonPublicBinding);

            var isEvent = eventField != null;

            var declaringType = field.DeclaringType;

            if (isEvent && !allowEvents || !((aspect.Target & WeaveTargetType.Fields) == WeaveTargetType.Fields))
                return false;
            
            return IsValidAspectFor(aspect, fieldName, declaringType, fieldName, field.IsPublic);
        }

        /// <summary>
        /// Determine if aspect should be applied to the given method
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="aspect">Aspect</param>
        /// <returns></returns>
        internal static bool IsValidAspectFor(MethodBase method, AspectBase aspect)
        {
            var methodName = method.Name;

            var declaringType = method.DeclaringType;

            var isProperty = methodName.StartsWith("get_") || methodName.StartsWith("set_");

            var isEvent = methodName.StartsWith("add_") || methodName.StartsWith("remove_");

            var searchName = isProperty ? methodName.Substring(4) : methodName;

            var isPublic = method.IsPublic;

            if (isProperty && !((aspect.Target & WeaveTargetType.Properties) == WeaveTargetType.Properties))
                return false;

            if (isEvent && !((aspect.Target & WeaveTargetType.Events) == WeaveTargetType.Events))
                return false;

            if ((!isProperty && !isEvent) && !((aspect.Target & WeaveTargetType.Methods) == WeaveTargetType.Methods))
                return false;

            return IsValidAspectFor(aspect, methodName, declaringType, searchName, isPublic);
        }

        /// <summary>
        /// Determine if aspect should be applied to the given context
        /// </summary>
        /// <param name="aspect">Aspect</param>
        /// <param name="originalName">Original Name</param>
        /// <param name="declaringType">Declaring Type</param>
        /// <param name="searchName">Search Name</param>
        /// <param name="isPublic">Is Public</param>
        /// <returns></returns>
        private static bool IsValidAspectFor(AspectBase aspect, string originalName, Type declaringType, string searchName, bool isPublic)
        {
            var fullName = declaringType.FullName;

            var searchTypePattern = aspect.SearchTypePattern;

            var searchMemberPattern = aspect.SearchMemberPattern;

            var modifier = aspect.Modifier;

            var target = aspect.Target;

            if (isPublic && !((modifier & WeaveAccessModifier.Public) == WeaveAccessModifier.Public))
                return false;

            if (!isPublic && !((modifier & WeaveAccessModifier.NonPublic) == WeaveAccessModifier.NonPublic))
                return false;

            if (!string.IsNullOrWhiteSpace(searchMemberPattern))
            {
                if (!Regex.IsMatch(originalName, searchMemberPattern) && !Regex.IsMatch(searchName, searchMemberPattern))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(searchTypePattern) && !Regex.IsMatch(fullName, searchTypePattern))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Modify type methods to include registered weaved method code
        /// </summary>
        /// <param name="classType">Class Type</param>
        /// <returns>Type</returns>
        private static Type WeaveType(Type classType)
        {
            var name = classType.FullName;

            var asmAspects = classType.Assembly.GetCustomAttributes<AspectBase>();
            
            var typeAspects = GetValidAspects(classType, asmAspects);

            var type = moduleBuilder.DefineType(name, TypeAttributes.Public | TypeAttributes.Serializable | TypeAttributes.Sealed, typeof(object));
            
            var sctor = type.DefineConstructor(MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);

            var sil = sctor.GetILGenerator();

            var fieldAspects = classType.GetFields(NonPublicBinding).Select(x => new { Field = x, Aspects = typeAspects.Where(a => IsValidAspectFor(x, a)) })
                .Where(x => x.Aspects.Any())
                .SelectMany(x => x.Field.GetCustomAttributes<AspectBase>().Where(y => !y.Exclude).Union(x.Aspects)).Distinct().ToList();

            var methods = classType.GetMethods(NonPublicBinding).Where(x => x.DeclaringType != typeof(object) && x.DeclaringType == classType);

            //Validate aspect for given type
            foreach (var typeAspect in typeAspects)
                typeAspect.ValidateRules(classType, methods);

            var ctors = classType.GetConstructors(NonPublicBinding);

            var list = new List<MethodBase>();
            
            // Define Methods
            foreach (MethodBase method in methods)
            {
                DefineWeaveMethod(classType, typeAspects, type, sil, fieldAspects, list, method);
            }

            // Define Constructors
            //foreach (var ctor in ctors)
            //{
                //DefineWeaveMethod(classType, typeAspects, type, sil, fieldAspects, list, ctor);
            //}

            // Define default ctor is not already defined
            if (!list.Any(x => x.IsConstructor && !x.GetParameters().Any()))
                type.DefineDefaultConstructor(MethodAttributes.Public);
            
            sil.Emit(OpCodes.Ret);

            var returnType = type.CreateType();

            foreach (var item in list)
            {
                var newMethod = GetWeavedMethod(returnType, item, useTemp: false);

                if (newMethod != null)
                    item.SwapWith(newMethod);
            }

            return returnType;
        }

        /// <summary>
        /// Define weaved method
        /// </summary>
        /// <param name="classType">ClassType</param>
        /// <param name="typeAspects">TypeAspects</param>
        /// <param name="type">Type</param>
        /// <param name="sil">Static ILGenerator</param>
        /// <param name="fieldAspects">Field Aspects</param>
        /// <param name="list">Method Bases</param>
        /// <param name="method">Method</param>
        private static void DefineWeaveMethod(Type classType, List<AspectBase> typeAspects, TypeBuilder type, ILGenerator sil, List<AspectBase> fieldAspects, List<MethodBase> list, MethodBase method)
        {
            var isConstructor = method.IsConstructor;
            var methodName = method.Name;

            var parameters = method.GetParameters();

            var isProperty = methodName.StartsWith("get_") || methodName.StartsWith("set_");

            var newMethodName = isProperty ? methodName.Substring(4) : methodName;

            var propInfo = isProperty ? classType.GetProperty(newMethodName, NonPublicBinding) : null;

            //Flag to make sure it is really a property and not a method starting with get_ or set_
            isProperty = propInfo != null;

            var aspectAttrs = (isProperty ? propInfo.GetCustomAttributes<AspectBase>() :
                method.GetCustomAttributes<AspectBase>()).Where(x => !x.Exclude);

            //Used for explictly attribute applied to getter and setter
            if (isProperty && !aspectAttrs.Any())
            {
                aspectAttrs = method.GetCustomAttributes<AspectBase>().Where(x => !x.Exclude);
            }

            var parameterAspects = parameters.SelectMany(x => x.GetCustomAttributes<AspectBase>()).Where(x => x != null).Where(x => !x.Exclude).ToList();

            var shouldOverride =
                (aspectAttrs.Any() || typeAspects.Any() || parameterAspects.Any() || fieldAspects.Any()) &&
                !method.IsGenericMethod &&
                !method.DeclaringType.IsSystemDefined();

            if (!shouldOverride)
            {
                return;
            }

            var aspectTypes = new List<Type>();

            var methAspects = new List<AspectBase>();

            foreach (var typeAspect in typeAspects)
            {
                if (IsValidAspectFor(method, typeAspect))
                {
                    methAspects.Add(typeAspect);
                    aspectTypes.Add(typeAspect.GetType());
                }
            }

            foreach (var methAspect in aspectAttrs)
            {
                var aspectType = methAspect.GetType();
                var aspectIndex = aspectTypes.IndexOf(aspectType);
                if (aspectIndex < 0)
                {
                    methAspects.Add(methAspect);
                    aspectTypes.Add(aspectType);
                }
            }

            var analysises = aspectTypes.Select(x => GetCachedILAnalysis(x)).ToList();

            var hasAdditionalAspects = parameterAspects.Any() || fieldAspects.Any() ||
                methAspects.Any(x => x.OnBeginAspectBlock != null || x.OnEndAspectBlock != null);

            var allEmptyMethods = analysises.All(x => x.EmptyExceptionMethod && x.EmptyExitMethod && x.EmptyInterceptMethod && x.EmptySuccessMethod)
                && !hasAdditionalAspects;

            if (allEmptyMethods || (!methAspects.Any() && !hasAdditionalAspects))
            {
                return;
            }

            var methodReturnType = isConstructor ? typeof(void) : (method as MethodInfo).ReturnType;

            var isVoid = methodReturnType == typeof(void);

            var methodParameters = parameters.Select(p => p.ParameterType).ToArray();

            var isStatic = method.IsStatic;

            var methodInfoField = type.DefineField(string.Concat("__<info>_", method.Name, counter++), typeof(MethodBase), FieldAttributes.Static | FieldAttributes.Private);
            var methodAttrField = type.DefineField(string.Concat("__<attr>_", method.Name, counter++), typeof(List<Attribute>), FieldAttributes.Static | FieldAttributes.Private);

            if (isConstructor)
                sil.Emit(OpCodes.Ldtoken, method as ConstructorInfo);
            else
                sil.Emit(OpCodes.Ldtoken, method as MethodInfo);

            sil.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
            sil.Emit(OpCodes.Stsfld, methodInfoField);

            if (isConstructor)
                sil.Emit(OpCodes.Ldtoken, method as ConstructorInfo);
            else
                sil.Emit(OpCodes.Ldtoken, method as MethodInfo);

            sil.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
            sil.Emit(OpCodes.Call, GetMethodAttributesMethod);
            sil.Emit(OpCodes.Stsfld, methodAttrField);

            var meth = isConstructor ? 
                (MethodBase)type.DefineConstructor(method.Attributes, method.CallingConvention, methodParameters)
                : type.DefineMethod(methodName, method.Attributes, method.CallingConvention, methodReturnType, methodParameters);

            if (!isConstructor)
                MakeMethodGenericIfNeeded(method, meth as MethodBuilder);

            var il = isConstructor ? (meth as ConstructorBuilder).GetILGenerator() :
                (meth as MethodBuilder).GetILGenerator();

            if (isConstructor)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, ObjectCtor);
            }

            var local = !isVoid ? il.DeclareLocal(methodReturnType) : null;

            WeaveMethod(type, method, methAspects, il, local, methodInfoField, methodAttrField, sil, aspectTypes, parameterAspects, methodParameters, fieldAspects);

            if (local != null)
            {
                il.Emit(OpCodes.Ldloc, local);
            }

            il.Emit(OpCodes.Ret);

            list.Add(method);
        }

        /// <summary>
        /// Get method already defined for a given type builder
        /// </summary>
        /// <param name="typeBuilder">TypeBuilder</param>
        /// <param name="name">Name</param>
        /// <param name="returnType">ReturnType</param>
        /// <param name="parameterTypes">ParameterTypes</param>
        /// <returns></returns>
        internal static MethodBuilder GetMethodEx(this TypeBuilder typeBuilder, string name, Type returnType, Type[] parameterTypes)
        {
            var key = string.Format("{0}_{1}_{2}", name, returnType.FullName, string.Join(",", parameterTypes.Select(x => x.FullName)));
            MethodBuilder method;

            if(DefinedMethods.TryGetValue(key, out method))
            {
                return method;
            }

            return method;
        }

        /// <summary>
        /// Define method and track defined method
        /// </summary>
        /// <param name="typeBuilder">TypeBuilder</param>
        /// <param name="name">Name</param>
        /// <param name="attributes">Attributes</param>
        /// <param name="callingConvention">CallingConvention</param>
        /// <param name="returnType">ReturnType</param>
        /// <param name="parameterTypes">ParameterTypes</param>
        /// <returns></returns>
        internal static MethodBuilder DefineMethodEx(this TypeBuilder typeBuilder, string name, MethodAttributes attributes, CallingConventions callingConvention, Type returnType, Type[] parameterTypes)
        {
            var key = string.Format("{0}_{1}_{2}", name, returnType.FullName, string.Join(",", parameterTypes.Select(x => x.FullName)));
            return DefinedMethods[key] = typeBuilder.DefineMethod(name, attributes, callingConvention, returnType, parameterTypes);
        }

        /// <summary>
        /// Generate Replacement Method for debug mode
        /// </summary>
        /// <param name="type">TypeBuilder</param>
        /// <param name="method">MethodInfo</param>
        /// <param name="methAttr">Method Attribute</param>
        /// <param name="methodReturnType">Method Return Type</param>
        /// <param name="methodParameters">Method Parameters</param>
        /// <param name="sil">Static ILGenerator</param>
        /// <param name="aspects">Aspects</param>
        /// <param name="parameterAspects">Parameters Aspects</param>
        /// <param name="methodContext">Method Context</param>
        /// <returns>MethodBuilder</returns>
        private static MethodBuilder GenerateReplacementMethod(TypeBuilder type, MethodBase method, MethodAttributes methAttr, Type methodReturnType, Type[] methodParameters, ILGenerator sil, List<AspectBase> aspects, List<AspectBase> parameterAspects, LocalBuilder methodContext)
        {
            var meth = type.DefineMethodEx(string.Concat(method.Name, "_"), methAttr, method.CallingConvention, methodReturnType, methodParameters);
            var parameters = method.GetParameters();
            MakeMethodGenericIfNeeded(method, meth);

            foreach(var customAttr in method.CustomAttributes)
            {
                var fields = customAttr.NamedArguments.Where(x => x.IsField);
                var properties = customAttr.NamedArguments.Where(x => !x.IsField);
                if (!fields.Any() && !properties.Any())
                    meth.SetCustomAttribute(new CustomAttributeBuilder(customAttr.Constructor, customAttr.ConstructorArguments.Select(x => x.Value).ToArray()));
                else if (fields.Any())
                    meth.SetCustomAttribute(new CustomAttributeBuilder(customAttr.Constructor, 
                        customAttr.ConstructorArguments.Select(x => x.Value).ToArray(), 
                        fields.Select(x => (FieldInfo)x.MemberInfo).ToArray(),
                        fields.Select(x => x.TypedValue.Value).ToArray()));
                else if (properties.Any())
                    meth.SetCustomAttribute(new CustomAttributeBuilder(customAttr.Constructor, 
                        customAttr.ConstructorArguments.Select(x => x.Value).ToArray(), 
                        properties.Select(x => (PropertyInfo)x.MemberInfo).ToArray(),
                        properties.Select(x => x.TypedValue.Value).ToArray()));
            }

            meth.SetImplementationFlags(method.MethodImplementationFlags);

            var il = meth.GetILGenerator();

            try
            {
                for(var i = 0; i < parameterAspects.Count; i++)
                {
                    var aspect = parameterAspects[i];
                    var parameter = parameters[i];
                    var beginBlock = aspect.OnBeginAspectBlock;
                    if (beginBlock != null)
                    {
                        beginBlock(type, method, parameter, il);
                    }
                }

                foreach(var aspect in aspects)
                {
                    var beginBlock = aspect.OnBeginAspectBlock;
                    if (beginBlock != null)
                    {
                        beginBlock(type, method, null, il);
                    }
                }

                ILWeaverUtil.CopyIL(type, method, il, type.Module, aspects, methodContext, sil);

                for (var i = 0; i < parameterAspects.Count; i++)
                {
                    var aspect = parameterAspects[i];
                    var parameter = parameters[i];
                    var endBlock = aspect.OnEndAspectBlock;
                    if (endBlock != null)
                    {
                        endBlock(type, method, parameter, il);
                    }
                }

                foreach (var aspect in aspects)
                {
                    var endBlock = aspect.OnEndAspectBlock;
                    if (endBlock != null)
                    {
                        endBlock(type, method, null, il);
                    }
                }
                il.Emit(OpCodes.Ret);
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Error weaving methods for {0}.{1}", method.DeclaringType.FullName, method.Name), ex);
            }

            return meth;
        }

        /// <summary>
        /// Return defined weaved method
        /// </summary>
        /// <param name="returnType">Type containing methods</param>
        /// <param name="item">MethodInfo for metadata</param>
        /// <param name="useTemp">Use Temporary weaved method</param>
        /// <returns>MethodInfo</returns>
        private static MethodBase GetWeavedMethod(Type returnType, MethodBase item, bool useTemp)
        {
            MethodBase newMethod = null;
            var itemParameters = item.GetParameters();
            var itemName = item.Name + (useTemp ? "_" : string.Empty);
            if (item.IsGenericMethod)
            {
                newMethod = (item.IsConstructor ? 
                    returnType.GetConstructors(NonPublicBinding).Cast<MethodBase>() 
                    : returnType.GetMethods(NonPublicBinding)).FirstOrDefault(x => x.Name == itemName &&
                 x.IsGenericMethod && x.GetParameters().All(p => itemParameters.Any(y => y.ParameterType.Name == p.ParameterType.Name)));
                if (newMethod == null)
                {
                    newMethod = returnType.GetMethod(itemName);
                }
            }
            else
            {
                newMethod = item.IsConstructor ? 
                    (MethodBase)returnType.GetConstructor(NonPublicBinding & ~BindingFlags.Static, null, itemParameters.Select(x => x.ParameterType).ToArray(), null)
                    : returnType.GetMethod(itemName, NonPublicBinding, null, itemParameters.Select(x => x.ParameterType).ToArray(), null);
            }

            return newMethod;
        }

        /// <summary>
        /// Add Generic properties to current method builder if needed
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="meth">MethodBuilder</param>
        private static void MakeMethodGenericIfNeeded(MethodBase method, MethodBuilder meth)
        {
            if (!method.IsGenericMethod)
                return;
            
            var genericParameters = method.GetGenericArguments();
            var generics = meth.DefineGenericParameters(genericParameters.Select(x => x.Name).ToArray());

            for (var i = 0; i < generics.Length; i++)
            {
                generics[i].SetGenericParameterAttributes(genericParameters[i].GenericParameterAttributes);

                var constraints = genericParameters[i].GetGenericParameterConstraints();
                var interfaces = new List<Type>(constraints.Length);
                foreach (var constraint in constraints)
                {
                    if (constraint.IsClass)
                    {
                        generics[i].SetBaseTypeConstraint(constraint);
                    }
                    else
                    {
                        interfaces.Add(constraint);
                    }
                }

                generics[i].SetInterfaceConstraints(interfaces.ToArray());
            }
        }
    }
}
