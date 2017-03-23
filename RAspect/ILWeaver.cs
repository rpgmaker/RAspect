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
using Mono.Cecil.Rocks;

namespace RAspect
{
    /// <summary>
    /// IL Weaver for weaving method/types with various functionalities
    /// </summary>
    public static class ILWeaver
    {
        /// <summary>
        /// Binding for non public
        /// </summary>
        internal static readonly BindingFlags NonPublicBinding = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Dictionary for keeping track of cil analysis
        /// </summary>
        private static readonly ConcurrentDictionary<string, ILAnalysis> AspectAnalysises = new ConcurrentDictionary<string, ILAnalysis>();

        /// <summary>
        /// Dictionary for keeping track of defined methods
        /// </summary>
        internal static readonly ConcurrentDictionary<string, Mono.Cecil.MethodDefinition> DefinedMethodDefs = new ConcurrentDictionary<string, Mono.Cecil.MethodDefinition>();
        
        /// <summary>
        /// Dictionary for keeping track of fields definition of aspects
        /// </summary>
        internal static readonly ConcurrentDictionary<string, Dictionary<string, Mono.Cecil.FieldDefinition>> TypeFieldAspects = new ConcurrentDictionary<string, Dictionary<string, Mono.Cecil.FieldDefinition>>();

        /// <summary>
        /// Dictionary for keeping track of fields definition of aspect wrappers
        /// </summary>
        internal static readonly ConcurrentDictionary<string, Dictionary<string, Mono.Cecil.FieldDefinition>> TypeFieldAspectWrappers = new ConcurrentDictionary<string, Dictionary<string, Mono.Cecil.FieldDefinition>>();

        /// <summary>
        /// Dictionary for keeping track of weaved type flag
        /// </summary>
        private static readonly ConcurrentDictionary<Mono.Cecil.TypeReference, bool> TypeWeavedFlags = new ConcurrentDictionary<Mono.Cecil.TypeReference, bool>();
        
        /// <summary>
        /// Aspect type
        /// </summary>
        private static readonly Type AspectType = typeof(AspectWrapper);

        /// <summary>
        /// AspectWrapper Constructor
        /// </summary>
        private static readonly ConstructorInfo AspectWrapperCtor = AspectType.GetConstructors().FirstOrDefault();

        /// <summary>
        /// Method Context type
        /// </summary>
        private static readonly Type MethodContextType = typeof(MethodContext);
        
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
        /// MethodContext Proceed Property
        /// </summary>
        private static readonly PropertyInfo MethodContextProceed = MethodContextType.GetProperty("Proceed");

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
        /// Collection of cached aspect instances
        /// </summary>
        private static readonly ConcurrentDictionary<Type, AspectBase> Aspects = new ConcurrentDictionary<Type, AspectBase>();

        /// <summary>
        /// Constructor info for MethodContext ArgumentInfo
        /// </summary>
        private static ConstructorInfo MethodParameterContextCtor = typeof(MethodParameterContext).GetConstructor(new[] { typeof(string), typeof(bool) });

        /// <summary>
        /// Prefix counter for fields/method definitions
        /// </summary>
        private static long counter = 0;

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
        /// <param name="module">Module</param>
        public static void Weave(Mono.Cecil.ModuleDefinition module)
        {
            var assembly = module.Assembly;

            var asmAttrs = assembly.GetCustomAttributes<AspectBase>();
            var types = module.GetTypes().ToList();

            var type = new Mono.Cecil.TypeDefinition("RAspectImplementation", "__<>_Implementation", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, module.TypeSystem.Object);

            var sctor = new Mono.Cecil.MethodDefinition(".cctor", Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            var body = sctor.Body;

            body.InitLocals = true;
            body.OptimizeMacros();

            type.Methods.Add(sctor);

            module.Types.Add(type);

            var sil = sctor.Body.GetILProcessor();

            foreach (var classType in types)
            {
                WeaveType(classType, asmAttrs, type, sil);
            }

            sil.Emit(Mono.Cecil.Cil.OpCodes.Ret);
        }

        /// <summary>
        /// Determine if target type is a subclass of clazz
        /// </summary>
        /// <param name="target"></param>
        /// <param name="clazz"></param>
        /// <returns></returns>
        private static bool IsSubClassOf(this Mono.Cecil.TypeReference target, Mono.Cecil.TypeReference clazz)
        {
            if(target.FullName == clazz.FullName)
            {
                return true;
            }

            var baseType = target.Resolve().BaseType;

            while(baseType != null)
            {
                if(baseType.FullName == clazz.FullName)
                {
                    return true;
                }

                baseType = baseType.Resolve().BaseType;
            }

            return false;
        }

        /// <summary>
        /// Determine if the given type has aspect
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="asmAspects">Assembly</param>
        /// <returns>Bool</returns>
        private static bool HasAspect(Mono.Cecil.TypeDefinition type, IEnumerable<AspectBase> asmAspects = null)
        {
            bool success = false;

            if (type == null || type.Name.Contains("Anonymous"))
            {
                return false;
            }

            if (TypeWeavedFlags.TryGetValue(type, out success))
            {
                return success;
            }

            var typeAspects = GetValidAspects(type, asmAspects);

            if (typeAspects.Any() && typeAspects.All(x => x.Exclude))
            {
                return TypeWeavedFlags[type] = false;
            }

            var methods = type.Methods.Where(x => x.DeclaringType == type);

            var props = type.Properties.Where(x => x.DeclaringType == type);

            var fields = type.Fields.Where(x => x.DeclaringType == type);

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

            return success = TypeWeavedFlags[type] = typeAspects.Any();
        }

        /// <summary>
        /// Get applicable aspect for the given type and include assembly aspect if available
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="asmAspects">Optional Assembly Aspects</param>
        /// <returns></returns>
        private static List<AspectBase> GetValidAspects(Mono.Cecil.TypeReference type, IEnumerable<AspectBase> asmAspects = null)
        {
            var aspectAttributes = type.Resolve().GetCustomAttributes<AspectBase>().Where(x => !x.Exclude).ToList();

            var fullName = type.FullName;

            var assemblyAspects = (asmAspects ?? type.Module.Assembly.GetCustomAttributes<AspectBase>()).Where(x => !x.Exclude);

            foreach (var aspect in assemblyAspects)
            {
                var searchPattern = aspect.SearchTypePattern;
                var isValid = aspect.Target != 0 && (string.IsNullOrWhiteSpace(searchPattern) ||
                    searchPattern.ToRegex().IsMatch(fullName));

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
        private static ILAnalysis GetCachedILAnalysis(Mono.Cecil.TypeReference aspectType)
        {
            return AspectAnalysises.GetOrAdd(aspectType.FullName, _ => GetILAnalysis(aspectType));
        }

        /// <summary>
        /// Get aspect information regarding entry/exit/exception
        /// </summary>
        /// <param name="aspectType">Aspect Type</param>
        /// <returns>ILAnalysis</returns>
        private static ILAnalysis GetILAnalysis(Mono.Cecil.TypeReference aspectType)
        {
            var aspectTypeBase = typeof(AspectBase).ToCecil();
            var methods = aspectType.GetMethods().Where(x => 
                x.DeclaringType.IsSubClassOf(aspectTypeBase) && (x.CustomAttributes.Count > 0 || x.DeclaringType.FullName == aspectType.FullName));
            
            var analysis = new ILAnalysis();
            var @proceed = MethodContextProceed.GetSetMethod().ToCecil();
            var @proceedGet = MethodContextProceed.GetGetMethod().ToCecil();
            var arguments = MethodContextArguments.GetGetMethod().ToCecil();
            var instance = MethodContextInstance.GetGetMethod().ToCecil();
            var meth = MethodContextMethod.GetGetMethod().ToCecil();
            var @return = MethodContextReturns.GetGetMethod().ToCecil();
            var @break = false;

            foreach (var method in methods)
            {
                var entryPoint = (method.GetBaseMethod() ?? method).GetCustomAttribute<EntryPointAttribute>();
                
                if (entryPoint == null || !method.HasBody)
                {
                    continue;
                }

                var instructions = method.Body.Instructions;
                foreach(var instruction in instructions)
                {
                    var instructionValue = instruction.OpCode.Value;

                    if (instructionValue == Mono.Cecil.Cil.OpCodes.Nop.Value)
                    {
                        continue;
                    }

                    var propMethod = instruction.Operand as Mono.Cecil.MethodReference;

                    if (instructionValue != Mono.Cecil.Cil.OpCodes.Ret.Value)
                    {
                        if (entryPoint.Type == EntryPointType.Enter)
                        {
                            analysis.EmptyInterceptMethod = false;
                        }

                        if (entryPoint.Type == EntryPointType.Exit)
                        {
                            analysis.EmptyExitMethod = false;
                        }

                        if (entryPoint.Type == EntryPointType.Error)
                        {
                            analysis.EmptyExceptionMethod = false;
                        }

                        if (entryPoint.Type == EntryPointType.Success)
                        {
                            analysis.EmptySuccessMethod = false;
                        }
                    }

                    if (propMethod == null)
                    {
                        continue;
                    }

                    analysis.ProceedUsed = analysis.ProceedUsed || (@proceed.FullName == propMethod.FullName || @proceedGet.FullName == propMethod.FullName);
                    analysis.ArgumentsUsed = analysis.ArgumentsUsed || arguments.FullName == propMethod.FullName;
                    analysis.InstanceUsed = analysis.InstanceUsed || instance.FullName == propMethod.FullName;
                    analysis.MethodUsed = analysis.MethodUsed || meth.FullName == propMethod.FullName;
                    analysis.ReturnUsed = analysis.ReturnUsed || @return.FullName == propMethod.FullName;

                    if (analysis.ProceedUsed && analysis.ArgumentsUsed && analysis.InstanceUsed && analysis.MethodUsed && analysis.ReturnUsed)
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
        /// <param name="copy">Method Copy</param>
        private static void WeaveMethod(Mono.Cecil.TypeDefinition type, Mono.Cecil.MethodDefinition method, List<AspectBase> aspectAttributes, Mono.Cecil.Cil.ILProcessor il, Mono.Cecil.Cil.VariableDefinition local, Mono.Cecil.FieldDefinition methodInfoField, Mono.Cecil.FieldDefinition methodAttrField, Mono.Cecil.Cil.ILProcessor sil, List<Mono.Cecil.TypeReference> aspectTypes, List<AspectBase> parameterAspects, Mono.Cecil.TypeReference[] methodParameters, List<AspectBase> fieldAspects, Mono.Cecil.MethodDefinition copy)
        {
            var module = type.Module;
            var declaringType = method.DeclaringType;
            var argumentsField = type.DefineField(string.Concat("_<args>_", method.Name, counter++), typeof(MethodParameterContext[]), Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Public);
            var fields = TypeFieldAspects.GetOrAdd(declaringType.FullName, _ => new Dictionary<string, Mono.Cecil.FieldDefinition>());
            var fieldWrappers = TypeFieldAspectWrappers.GetOrAdd(declaringType.FullName, _ => new Dictionary<string, Mono.Cecil.FieldDefinition>());
            var parameters = method.Parameters;


            foreach (var aspectType in aspectTypes.Union(fieldAspects.Select(x => x.GetType().ToCecil())).Union(parameterAspects.Select(x => x.GetType().ToCecil())))
            {
                var key = aspectType.FullName;
                Mono.Cecil.FieldDefinition staticField = null;

                if (!fields.TryGetValue(key, out staticField))
                {
                    var aspectTypeType = aspectType.ReflectionType();
                    
                    //Define Aspect
                    fields[key] = staticField = type.DefineField(string.Concat("_<aspect>", key), aspectTypeType, Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static);

                    //Define AspectWrapper
                    fieldWrappers[key] = type.DefineField(string.Concat("_<aspect_wrapper>", key), AspectType, Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static);

                    sil.Emit(Mono.Cecil.Cil.OpCodes.Call, GetAspectValue.MakeGenericMethod(aspectTypeType));
                    sil.Emit(Mono.Cecil.Cil.OpCodes.Dup);
                    sil.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, staticField);
                    
                    sil.Emit(Mono.Cecil.Cil.OpCodes.Newobj, AspectWrapperCtor);
                    sil.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, fieldWrappers[key]);
                }
            }
            
            for (var i = 0; i < parameterAspects.Count; i++)
            {
                var aspect = parameterAspects[i];
                var parameter = parameters[i];
                var beginBlock = aspect.OnBeginBlock;
                if (beginBlock != null)
                {
                    beginBlock(declaringType, method, parameter, il);
                }
            }

            foreach (var aspect in aspectAttributes)
            {
                var beginBlock = aspect.OnBeginBlock;
                if (beginBlock != null)
                {
                    beginBlock(declaringType, method, null, il);
                }
            }
            
            var isStatic = method.IsStatic;
            var parameterOffset = isStatic ? 0 : 1;

            var analysises = aspectTypes.Select(x => GetCachedILAnalysis(x)).ToList();
            var proceedUsed = analysises.Any(x => x.ProceedUsed);
            var methodUsed = analysises.Any(x => x.MethodUsed);
            var argumentsUsed = analysises.Any(x => x.ArgumentsUsed);
            var instanceUsed = analysises.Any(x => x.InstanceUsed);
            var returnUsed = analysises.Any(x => x.ReturnUsed);
            var usesNone = !(proceedUsed || methodUsed || argumentsUsed || instanceUsed || returnUsed);
            var needTryCatch = analysises.Any(x => !x.EmptyExceptionMethod || !x.EmptyExitMethod);

            var count = parameters.Where(x => !x.IsOut).Count();

            var methodContext = usesNone ? null : il.DeclareLocal(typeof(MethodContext));
            var argumentValues = count > 0 && argumentsUsed ? il.DeclareLocal(typeof(object[])) : null;

            var proceedLabel = proceedUsed ? il.DefineLabel() : default(Mono.Cecil.Cil.Instruction);
            var notProceedLabel = proceedUsed ? il.DefineLabel() : default(Mono.Cecil.Cil.Instruction);
            var proceedLocal = proceedUsed ? il.DeclareLocal(typeof(bool)) : null;
            var ex = il.DeclareLocal(typeof(Exception));

            var index = 0;

            if (argumentValues != null)
            {
                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, count);
                sil.Emit(Mono.Cecil.Cil.OpCodes.Newarr, typeof(MethodParameterContext));
                sil.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, argumentsField);

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, count);
                il.Emit(Mono.Cecil.Cil.OpCodes.Newarr, typeof(object));
                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, argumentValues);
            }

            for (var i = 0; argumentsUsed && i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter.IsOut)
                {
                    continue;
                }

                var parameterType = parameter.ParameterType;

                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, argumentsField);
                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, index);
                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, parameter.Name);

                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, parameterType.IsByReference ? 1 : 0);
                sil.Emit(Mono.Cecil.Cil.OpCodes.Newobj, module.Import(MethodParameterContextCtor));
                sil.Emit(Mono.Cecil.Cil.OpCodes.Stelem_Ref);

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, argumentValues);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, index);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, i + parameterOffset);
                if (parameterType.IsByReference)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldind_Ref);
                }

                if (parameterType.IsValueType || parameterType.IsGenericParameter)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Box, parameterType);
                }

                //Capture parameter attribute

                il.Emit(Mono.Cecil.Cil.OpCodes.Stelem_Ref);

                index++;
            }

            if (methodContext != null)
            {
                if (argumentValues != null)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, argumentsField);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, argumentValues);
                }
                else
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                }

                il.Emit(Mono.Cecil.Cil.OpCodes.Newobj, module.Import(methodContext.VariableType.GetConstructors().First()));
                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, methodContext);
            }

            if (!isStatic && (instanceUsed && methodContext != null))
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                if (declaringType.IsValueType)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldobj);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Box, declaringType);
                }

                il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextInstance.GetSetMethod());
            }

            if (methodUsed)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, methodInfoField);
                il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextMethod.GetSetMethod());

                var needAttrs = aspectAttributes.Any(x => x.BlockType == WeaveBlockType.Inline) ||
                    fieldAspects.Any(x => x.BlockType == WeaveBlockType.Inline) ||
                    parameterAspects.Any(x => x.BlockType == WeaveBlockType.Inline);

                if (needAttrs)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, methodAttrField);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextAttributes.GetSetMethod());
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

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, fieldWrappers[aspectType.FullName]);
                if (methodContext != null)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                }
                else
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                }

                il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, AspectEntry);
            }

            if (proceedUsed)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextProceed.GetGetMethod());
                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, proceedLocal);

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, proceedLocal);
                il.Emit(Mono.Cecil.Cil.OpCodes.Brfalse, proceedLabel);
            }

            if (needTryCatch)
                il.BeginExceptionBlock();

            var methodReturnType = method.IsConstructor ? method.DeclaringType.Module.TypeSystem.Void : method.ReturnType;

            InvokeCopyMethod(il, local, copy, isStatic, parameterOffset, parameters.ToList());

            if (local != null)
            {
                if (returnUsed && methodContext != null)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, local);
                    if (methodReturnType.IsValueType || methodReturnType.IsGenericParameter)
                    {
                        il.Emit(Mono.Cecil.Cil.OpCodes.Box, methodReturnType);
                    }

                    il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextReturns.GetSetMethod());
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

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, fieldWrappers[aspectType.FullName]);
                if (methodContext != null)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                }
                else
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                }

                il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, AspectSuccess);
            }

            if (local != null)
            {
                if (returnUsed && methodContext != null)
                {
                    var aspectNoReturnLabel = il.DefineLabel();
                    var localType = local.VariableType;

                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextReturns.GetGetMethod());
                    il.Emit(Mono.Cecil.Cil.OpCodes.Brfalse, aspectNoReturnLabel);

                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                    il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, MethodContextReturns.GetGetMethod());
                    if (localType.IsValueType)
                        il.Emit(Mono.Cecil.Cil.OpCodes.Unbox_Any, localType);
                    else
                        il.Emit(Mono.Cecil.Cil.OpCodes.Isinst, localType);

                    il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, local);

                    il.MarkLabel(aspectNoReturnLabel);
                }
            }

            if (needTryCatch)
            {
                il.BeginCatchBlock(ex.VariableType);

                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, ex);
            }

            for (var i = 0; needTryCatch && i < aspectTypes.Count; i++)
            {
                var aspectType = aspectTypes[i];
                var analysis = analysises[i];

                if (analysis.EmptyExceptionMethod)
                {
                    continue;
                }

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, fieldWrappers[aspectType.FullName]);
                if (methodContext != null)
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                }
                else
                {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                }

                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, ex);
                il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, AspectException);
            }

            if (needTryCatch)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Rethrow);

                il.BeginFinallyBlock();

                for (var i = 0; i < aspectTypes.Count; i++)
                {
                    var aspectType = aspectTypes[i];
                    var analysis = analysises[i];

                    if (analysis.EmptyExitMethod)
                    {
                        continue;
                    }

                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, fieldWrappers[aspectType.FullName]);
                    if (methodContext != null)
                    {
                        il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, methodContext);
                    }
                    else
                    {
                        il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                    }

                    il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, AspectExit);
                }

                il.EndExceptionBlock();
            }

            if (proceedUsed)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Br, notProceedLabel);

                il.MarkLabel(proceedLabel);

                if (local != null)
                {
                    var localType = local.VariableType;
                    if (localType.IsValueType)
                    {
                        il.Emit(Mono.Cecil.Cil.OpCodes.Ldloca, local);
                        il.Emit(Mono.Cecil.Cil.OpCodes.Initobj, localType);
                    }
                    else
                    {
                        if (localType.IsGenericParameter)
                        {
                            il.Emit(Mono.Cecil.Cil.OpCodes.Call, GetDefaultValueMethod.MakeGenericMethod(localType.ReflectionType()));
                        }
                        else
                        {
                            il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                        }

                        il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, local);
                    }
                }

                il.MarkLabel(notProceedLabel);
            }
        }

        /// <summary>
        /// Invoke original copied method
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="local">Local for return type methods</param>
        /// <param name="clonedMethod">Cloned Method</param>
        /// <param name="isStatic">Is Static</param>
        /// <param name="parameterOffset">Parameter Offses</param>
        /// <param name="parameters">Parameters</param>
        private static void InvokeCopyMethod(Mono.Cecil.Cil.ILProcessor il, Mono.Cecil.Cil.VariableDefinition local, Mono.Cecil.MethodReference clonedMethod, bool isStatic, int parameterOffset, List<Mono.Cecil.ParameterDefinition> parameters)
        {
            var declaringType = clonedMethod.DeclaringType;
            var isCall = isStatic || declaringType.IsValueType;
            if (!isStatic)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            }

            for (var i = 0; i < parameters.Count; i++)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, i + parameterOffset);
            }

            if (declaringType.HasGenericParameters || clonedMethod.HasGenericParameters)
            {
                var genericParameters = clonedMethod.GenericParameters;
                
                if (declaringType.HasGenericParameters)
                {
                    var genericType = declaringType.MakeGenericInstanceType(declaringType.GenericParameters.ToArray());
                    var hasThis = clonedMethod.HasThis;
                    var convention = clonedMethod.CallingConvention;
                    clonedMethod = new Mono.Cecil.MethodReference(clonedMethod.Name, clonedMethod.ReturnType, genericType);
                    clonedMethod.CallingConvention = convention;
                    clonedMethod.HasThis = hasThis;

                    foreach(var parameter in parameters)
                    {
                        clonedMethod.Parameters.Add(parameter);
                    }

                    foreach (var genParam in genericParameters)
                    {
                        clonedMethod.GenericParameters.Add(new Mono.Cecil.GenericParameter(genParam.Name, clonedMethod));
                    }
                }

                if (genericParameters.Any())
                {
                    var genericMethod = (Mono.Cecil.GenericInstanceMethod)(
                        clonedMethod = new Mono.Cecil.GenericInstanceMethod(clonedMethod));

                    foreach (var generic in genericParameters)
                    {
                        genericMethod.GenericArguments.Add(generic);
                    }
                }
            }

            il.Emit(isCall ? Mono.Cecil.Cil.OpCodes.Call : Mono.Cecil.Cil.OpCodes.Callvirt, clonedMethod);

            if (local != null)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, local);
            }
        }

        /// <summary>
        /// Get method/properties attributes for a given method
        /// </summary>
        /// <param name="method"></param>
        /// <returns>List{Attribute}</returns>
        public static List<Attribute> GetMethodAttributes(MethodBase method)
        {
            var declaringType = method.DeclaringType;

            var methodName = method.Name;

            var isProperty = methodName.StartsWith("get_") || methodName.StartsWith("set_");

            var newMethodName = isProperty ? methodName.Substring(4) : methodName;

            var propInfo = isProperty ? declaringType.GetProperty(newMethodName, NonPublicBinding) : null;

            //TODO: Union Assembly Attribute applicable to method
            return (isProperty ? propInfo.GetCustomAttributes<Attribute>() :
                method.GetCustomAttributes<Attribute>()).ToList();
        }

        /// <summary>
        /// Determine if aspect should be applied to the given field
        /// </summary>
        /// <param name="field">Field</param>
        /// <param name="aspect">Aspect</param>
        /// <returns></returns>
        internal static bool IsValidAspectFor(Mono.Cecil.FieldDefinition field, AspectBase aspect, bool allowEvents = false)
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
        internal static bool IsValidAspectFor(Mono.Cecil.MethodDefinition method, AspectBase aspect)
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
        private static bool IsValidAspectFor(AspectBase aspect, string originalName, Mono.Cecil.TypeDefinition declaringType, string searchName, bool isPublic)
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
                var searchRegex = searchMemberPattern.ToRegex();
                if (!searchRegex.IsMatch(originalName) && !searchRegex.IsMatch(searchName))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(searchTypePattern) && !searchTypePattern.ToRegex().IsMatch(fullName))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Modify type methods to include registered weaved method code
        /// </summary>
        /// <param name="classType">Type</param>
        /// <param name="asmAttrs">Assembly Attribute</param>
        /// <param name="aspectBaseType">Aspect Type</param>
        /// <param name="type">Static Type</param>
        /// <param name="sil">Static IL Processor</param>
        private static void WeaveType(Mono.Cecil.TypeDefinition classType, IEnumerable<AspectBase> asmAttrs, Mono.Cecil.TypeDefinition type, Mono.Cecil.Cil.ILProcessor sil)
        {
            if (classType.IsSubClassOf(typeof(AspectBase).ToCecil()) || 
                classType.Name.IndexOf("<module>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            var module = classType.Module;
            var typeSystem = module.TypeSystem;

            var name = classType.FullName;

            var asmAspects = asmAttrs;

            var typeAspects = GetValidAspects(classType, asmAspects);

            var fields = classType.GetFields(NonPublicBinding);

            foreach(var field in fields)
            {
                //Allow override constructors
                if((field.Attributes & Mono.Cecil.FieldAttributes.InitOnly) == Mono.Cecil.FieldAttributes.InitOnly)
                {
                    field.Attributes &= ~Mono.Cecil.FieldAttributes.InitOnly;
                }
            }

            var fieldAspects = fields.Select(x => new { Field = x, Aspects = typeAspects.Where(a => IsValidAspectFor(x, a)) })
                .Where(x => x.Aspects.Any())
                .SelectMany(x => x.Field.GetCustomAttributes<AspectBase>().Where(y => !y.Exclude).Union(x.Aspects)).Distinct().ToList();

            var methods = classType.GetMethods(NonPublicBinding).Where(x => !x.DeclaringType.FullName.Equals(typeof(object).ToCecil().FullName) 
                && x.DeclaringType.FullName == classType.FullName).ToList();

            //Validate aspect for given type
            foreach (var typeAspect in typeAspects)
                typeAspect.ValidateRules(classType, methods);

            var ctors = classType.GetConstructors(NonPublicBinding).ToList();
            
            // Define Methods
            foreach (var method in methods)
            {
                DefineWeaveMethod(classType, typeAspects, type, sil, fieldAspects, method);
            }

            // Define Constructors
            foreach (var ctor in ctors)
            {
                DefineWeaveMethod(classType, typeAspects, type, sil, fieldAspects, ctor);
            }
        }

        /// <summary>
        /// Make copy of defined method
        /// </summary>
        /// <param name="method">Method to clone</param>
        /// <param name="methodContext">Method Context</param>
        /// <param name="aspects">Aspects</param>
        /// <returns></returns>
        private static Mono.Cecil.MethodDefinition CopyMethod(Mono.Cecil.MethodDefinition method, List<AspectBase> aspects)
        {
            var type = method.DeclaringType;
            var key = string.Format("{0}_{1}_{2}", method.Name, method.ReturnType.FullName, string.Join(",", method.Parameters.Select(x => x.ParameterType.FullName)));

            var meth = DefinedMethodDefs[key] = new Mono.Cecil.MethodDefinition(method.Name + "~",
                 method.IsStatic ? Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Public :
                Mono.Cecil.MethodAttributes.Public, 
                method.ReturnType);
            meth.HasThis = method.HasThis;
            meth.CallingConvention = method.CallingConvention;
            meth.ExplicitThis = method.ExplicitThis;

            var body = meth.Body;
            var oldBody = method.Body;
            var il = body.GetILProcessor();

            var instructions = oldBody.Instructions;
            var count = instructions.Count;
            var startIndex = 0;

            foreach (var param in method.Parameters)
            {
                meth.Parameters.Add(new Mono.Cecil.ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }


            foreach (var generic in method.GenericParameters)
            {
                var genParam = new Mono.Cecil.GenericParameter(generic.Name, meth);
                genParam.Attributes = generic.Attributes;
                generic.Constraints.ToList().ForEach(gp => genParam.Constraints.Add(gp));
                generic.CustomAttributes.ToList().ForEach(ca => genParam.CustomAttributes.Add(ca));
                generic.GenericParameters.ToList().ForEach(gp => genParam.GenericParameters.Add(new Mono.Cecil.GenericParameter(gp.Name, meth)));
                genParam.HasDefaultConstructorConstraint = generic.HasDefaultConstructorConstraint;
                genParam.IsContravariant = generic.IsContravariant;
                genParam.IsCovariant = generic.IsCovariant;
                genParam.IsNonVariant = generic.IsNonVariant;
                meth.GenericParameters.Add(genParam);
            }

            foreach (var variable in method.Body.Variables)
            {
                meth.Body.Variables.Add(variable);
            }

            foreach (var attribute in method.CustomAttributes)
            {
                meth.CustomAttributes.Add(attribute);
            }

            var methodDeclaringType = method.DeclaringType;
            var isConstructor = method.IsConstructor && !methodDeclaringType.IsSequentialLayout && methodDeclaringType.IsClass;

            if (isConstructor)
            {
                for (var i = 0; i < count; i++)
                {
                    var instruction = instructions[i];
                    var data = (instruction.Operand as Mono.Cecil.MethodReference)?.Resolve();
                    if (instruction.OpCode == Mono.Cecil.Cil.OpCodes.Call && data != null && data.IsConstructor)
                    {
                        startIndex = i + 1;
                        break;
                    }
                }
            }

            instructions = new Mono.Collections.Generic.Collection<Mono.Cecil.Cil.Instruction>(instructions.Skip(startIndex).ToList());

            Mono.Cecil.FieldDefinition eventField = null;

            foreach (var instruction in instructions)
            {
                var data = instruction.Operand;
                var dataField = (data as Mono.Cecil.FieldReference)?.Resolve();
                var dataMethod = (data as Mono.Cecil.MethodReference)?.Resolve();
                var getter = dataField != null && instruction.OpCode.Name.StartsWith("ld", StringComparison.OrdinalIgnoreCase);

                if (dataField != null && dataField.FieldType.IsSubClassOf(typeof(Delegate).ToCecil()))
                {
                    eventField = dataField;
                }

                if (dataField != null)
                {
                    var instructionValue = instruction.OpCode.Value;
                    var dataFieldType = dataField.FieldType;
                    if (getter)
                    {
                        var isAddressLoad = instructionValue == OpCodes.Ldflda.Value;
                        var adrLocal = isAddressLoad ? il.DeclareLocal(dataFieldType) : null;

                        il.Append(instruction);
                        WeaveField(il, aspects, dataField, getter);

                        if (isAddressLoad)
                        {
                            il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, adrLocal);
                            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloca, adrLocal);
                        }
                    }
                    else
                    {
                        WeaveField(il, aspects, dataField, getter);
                        il.Append(instruction);
                    }
                }else if(dataMethod != null)
                {
                    WeaveEventInvoke(il, aspects, dataMethod, eventField);
                    var successes = new List<bool>();
                    foreach (var aspect in aspects)
                    {
                        var invoke = aspect.OnMethodCall;
                        if (invoke != null)
                        {
                            successes.Add(invoke(type, il, meth, dataMethod));
                        }
                    }

                    if (!successes.Any(x => x))
                    {
                        il.Append(instruction);
                    }
                }
                else
                {
                    il.Append(instruction);
                }
            }

            foreach (var handler in oldBody.ExceptionHandlers)
            {
                body.ExceptionHandlers.Add(handler);
            }

            body.InitLocals = true;
            body.OptimizeMacros();

            methodDeclaringType.Methods.Add(meth);

            return meth;
        }

        /// <summary>
        /// Weave event invoke
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="aspects">Aspects</param>
        /// <param name="methodContext">Method Context</param>
        /// <param name="method">Method</param>
        /// <param name="field">Field</param>
        private static void WeaveEventInvoke(Mono.Cecil.Cil.ILProcessor il, List<AspectBase> aspects, Mono.Cecil.MethodDefinition method, Mono.Cecil.FieldDefinition field)
        {
            var isEventInvoke = method.Name.Equals("Invoke") && method.DeclaringType.IsSubClassOf(method.DeclaringType.Module.Import(typeof(Delegate)));

            if (isEventInvoke)
            {
                foreach (var aspect in aspects)
                {
                    var del = aspect.OnBlockInvokeEvent;
                    if (del != null)
                    {
                        del(il, method, field);
                    }
                }
            }
        }

        /// <summary>
        /// Weave Field for aspect
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="aspects">Aspects</param>
        /// <param name="field">Field</param>
        /// <param name="getter">Is Getter Operation</param>
        /// <param name="methodContext">Method Context</param>
        private static void WeaveField(Mono.Cecil.Cil.ILProcessor il, List<AspectBase> aspects, Mono.Cecil.FieldDefinition field, bool getter)
        {
            if (field == null)
                return;

            foreach (var aspect in aspects)
            {
                var del = getter ? aspect.OnBlockGetField : aspect.OnBlockSetField;
                if (del != null)
                {
                    del(il, field);
                }
            }
        }

        /// <summary>
        /// Define weaved method
        /// </summary>
        /// <param name="classType">ClassType</param>
        /// <param name="typeAspects">TypeAspects</param>
        /// <param name="type">Type</param>
        /// <param name="sil">Static ILGenerator</param>
        /// <param name="fieldAspects">Field Aspects</param>
        /// <param name="method">Method</param>
        private static void DefineWeaveMethod(Mono.Cecil.TypeDefinition classType, List<AspectBase> typeAspects, Mono.Cecil.TypeDefinition type, Mono.Cecil.Cil.ILProcessor sil, List<AspectBase> fieldAspects, Mono.Cecil.MethodDefinition method)
        {
            var module = classType.Module;
            var typeSystem = module.TypeSystem;

            var methodDeclaringType = method.DeclaringType;
            var isConstructor = method.IsConstructor && !methodDeclaringType.IsSequentialLayout && methodDeclaringType.IsClass;
            var methodName = method.Name;

            var parameters = method.Parameters;

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
                !method.IsGenericInstance;

            if (!shouldOverride)
            {
                return;
            }

            var aspectTypes = new List<Mono.Cecil.TypeReference>();

            var methAspects = new List<AspectBase>();

            foreach (var typeAspect in typeAspects)
            {
                if (IsValidAspectFor(method, typeAspect))
                {
                    methAspects.Add(typeAspect);
                    aspectTypes.Add(typeAspect.GetType().ToCecil());
                }
            }

            foreach (var methAspect in aspectAttrs)
            {
                var aspectType = methAspect.GetType();
                var aspectTypeRef = aspectType.ToCecil();
                var aspectIndex = aspectTypes.IndexOf(aspectTypeRef);
                if (aspectIndex < 0)
                {
                    methAspects.Add(methAspect);
                    aspectTypes.Add(aspectTypeRef);
                }
            }

            var analysises = aspectTypes.Select(x => GetCachedILAnalysis(x)).ToList();

            var hasAdditionalAspects = parameterAspects.Any() || fieldAspects.Any() ||
                methAspects.Any(x => x.OnBeginBlock != null || x.OnEndBlock != null);

            var allEmptyMethods = analysises.All(x => x.EmptyExceptionMethod && x.EmptyExitMethod && x.EmptyInterceptMethod && x.EmptySuccessMethod)
                && !hasAdditionalAspects;

            if (allEmptyMethods || (!methAspects.Any() && !hasAdditionalAspects))
            {
                return;
            }

            var methodReturnType = isConstructor ? typeSystem.Void : method.ReturnType;

            var isVoid = methodReturnType.FullName == typeSystem.Void.FullName;

            var methodParameters = parameters.Select(p => p.ParameterType).ToArray();

            var isStatic = method.IsStatic;
            var declaringTypeGeneric = methodDeclaringType.HasGenericParameters;

            var methodInfoField = type.DefineField(string.Concat("__<info>_", method.Name, counter++), typeof(MethodBase), Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Public);
            var methodAttrField = type.DefineField(string.Concat("__<attr>_", method.Name, counter++), typeof(List<Attribute>), Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Public);

            sil.Emit(Mono.Cecil.Cil.OpCodes.Ldtoken, method);

            if (declaringTypeGeneric)
            {
                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldtoken, methodDeclaringType);
                sil.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }));
            }
            else
            {
                sil.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
            }

            sil.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, methodInfoField);

            sil.Emit(Mono.Cecil.Cil.OpCodes.Ldtoken, method);

            if (declaringTypeGeneric)
            {
                sil.Emit(Mono.Cecil.Cil.OpCodes.Ldtoken, methodDeclaringType);
                sil.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }));
            }
            else
            {
                sil.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
            }

            sil.Emit(Mono.Cecil.Cil.OpCodes.Call, GetMethodAttributesMethod);
            sil.Emit(Mono.Cecil.Cil.OpCodes.Stsfld, methodAttrField);

            var body = method.Body;
            var instructions = body.Instructions;
            var count = instructions.Count;
            var newBody = new Mono.Cecil.Cil.MethodBody(method);
            var il = newBody.GetILProcessor();
            var variables = body.Variables;
            var marked = new Dictionary<int, Mono.Cecil.Cil.VariableDefinition>();

            var copy = CopyMethod(method, methAspects);

            method.CustomAttributes.Clear();

            if (isConstructor)
            {
                for (var i = 0; i < count; i++)
                {
                    var instruction = instructions[i];
                    var data = (instruction.Operand as Mono.Cecil.MethodReference)?.Resolve();
                    var varDef = instruction.Operand as Mono.Cecil.Cil.VariableReference;
                    var token = instruction.OpCode.ToString();

                    if (token.IndexOf("stloc", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var index = Int32.Parse(token.Split('.')[1]);
                        marked[index] = variables[index];
                    }else if(varDef != null)
                    {
                        marked[varDef.Index] = varDef.Resolve();
                    }

                    if (instruction.OpCode == Mono.Cecil.Cil.OpCodes.Call && data != null && data.IsConstructor)
                    {
                        il.Append(instruction);
                        break;
                    }
                    il.Append(instruction);
                }
            }

            variables.Clear();

            foreach(var variable in marked.OrderBy(x => x.Key).Select(x => x.Value).ToList())
            {
                newBody.Variables.Add(new Mono.Cecil.Cil.VariableDefinition(variable.Name, variable.VariableType));
            }

            
            var local = !isVoid ? il.DeclareLocal(methodReturnType) : null;
            
            WeaveMethod(type, method, methAspects, il, local, methodInfoField, methodAttrField, sil, aspectTypes, parameterAspects, methodParameters, fieldAspects, copy);

            for (var i = 0; i < parameterAspects.Count; i++)
            {
                var aspect = parameterAspects[i];
                var parameter = parameters[i];
                var endBlock = aspect.OnEndBlock;
                if (endBlock != null)
                {
                    endBlock(methodDeclaringType, method, parameter, il);
                }
            }

            foreach (var aspect in methAspects)
            {
                var endBlock = aspect.OnEndBlock;
                if (endBlock != null)
                {
                    endBlock(methodDeclaringType, method, null, il);
                }
            }

            if (local != null)
            {
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, local);
            }

            il.Emit(Mono.Cecil.Cil.OpCodes.Ret);

            method.Body = newBody;
        }
        
        /// <summary>
        /// Get method already defined for a given type builder
        /// </summary>
        /// <param name="typeBuilder">TypeBuilder</param>
        /// <param name="name">Name</param>
        /// <param name="returnType">ReturnType</param>
        /// <param name="parameterTypes">ParameterTypes</param>
        /// <returns></returns>
        internal static Mono.Cecil.MethodDefinition GetMethodEx(this Mono.Cecil.TypeDefinition typeBuilder, string name, Mono.Cecil.TypeReference returnType, Mono.Cecil.TypeReference[] parameterTypes)
        {
            var key = string.Format("{0}_{1}_{2}", name, returnType.FullName, string.Join(",", parameterTypes.Select(x => x.FullName)));
            Mono.Cecil.MethodDefinition method;

            if (DefinedMethodDefs.TryGetValue(key, out method))
            {
                return method;
            }

            return method;
        }
    }
}
