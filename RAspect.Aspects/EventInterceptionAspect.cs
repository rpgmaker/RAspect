using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    /// <summary>
    /// Aspect for intercepting events
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Assembly)]
    public abstract class EventInterceptionAspect : AspectBase
    {
        /// <summary>
        /// Event Context Constructor
        /// </summary>
        private readonly static ConstructorInfo EventContextCtor = typeof(EventContext).GetConstructor(new[] { typeof(object), typeof(object), typeof(bool), typeof(object[]) });

        /// <summary>
        /// On Set Value Method
        /// </summary>
        private readonly static MethodInfo InvokeEventMethod = typeof(EventInterceptionAspect).GetMethod("OnInvokeEvent");

        /// <summary>
        /// Initializes a new instance of the <see cref="EventInterceptionAspect"/> class.
        /// </summary>
        public EventInterceptionAspect() : base(WeaveTargetType.Events | WeaveTargetType.Fields)
        {
            OnBlockInvokeEvent = BlockInvokeEvent;
        }

        /// <summary>
        /// Capture on enter of event
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnEntry(MethodContext context)
        {
            var method = context.Method;
            var isAdd = method.Name.StartsWith("add_");

            var eventContext = new EventContext
            {
                Instance = context.Instance,
                Value = context.Arguments.FirstOrDefault().Value,
                Proceed = context.Proceed
            };

            if (isAdd)
                OnAddHandler(eventContext);
            else
                OnRemoveHandler(eventContext);
 
            context.Proceed = eventContext.Proceed;
            context.Returns = eventContext.Value;
        }

        /// <summary>
        /// Capture add handler
        /// </summary>
        /// <param name="context">EventContext</param>
        public virtual void OnAddHandler(EventContext context) { }

        /// <summary>
        /// Capture remove handler
        /// </summary>
        /// <param name="context">EventContext</param>
        public virtual void OnRemoveHandler(EventContext context) { }

        /// <summary>
        /// Capture invoke event
        /// </summary>
        /// <param name="context">EventContext</param>
        public virtual void OnInvokeEvent(EventContext context) { }

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
        /// Weave event invoke
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="method">Method</param>
        /// <param name="field">Field</param>
        private void BlockInvokeEvent(Mono.Cecil.Cil.ILProcessor il, Mono.Cecil.MethodDefinition method, Mono.Cecil.FieldDefinition field)
        {
            var isStatic = method.IsStatic;
            var eventContext = il.DeclareLocal(typeof(EventContext));
            var locals = new List<Mono.Cecil.Cil.VariableDefinition>();
            var objLocal = il.DeclareLocal(typeof(object[]));
            var parameters = method.Parameters;
            var parameterLength = parameters.Count;

            var fieldDeclaringType = field.DeclaringType;
            var aspect = field.GetCustomAttribute<EventInterceptionAspect>() ?? fieldDeclaringType.GetCustomAttribute<EventInterceptionAspect>()
                ?? fieldDeclaringType.Module.Assembly.GetCustomAttribute<EventInterceptionAspect>();

            if (!ILWeaver.IsValidAspectFor(field, aspect, allowEvents: true))
                return;
            
            var aspectField = ILWeaver.TypeFieldAspects[fieldDeclaringType.FullName][aspect.GetType().FullName];

            il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, parameterLength);
            il.Emit(Mono.Cecil.Cil.OpCodes.Newarr, typeof(object));
            il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, objLocal);

            for (var i = 0; i < parameterLength; i++)
            {
                var parameter = parameters[i];
                var parameterType = parameter.ParameterType;
                var local = il.DeclareLocal(parameterType);

                il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, local);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, objLocal);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, parameterLength - i - 1);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, local);
                if (parameterType.IsValueType)
                    il.Emit(Mono.Cecil.Cil.OpCodes.Box, parameterType);
                il.Emit(Mono.Cecil.Cil.OpCodes.Stelem_Ref);

                locals.Add(local);
            }

            if (!isStatic)
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, 1);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, objLocal);
            il.Emit(Mono.Cecil.Cil.OpCodes.Newobj, EventContextCtor);
            il.Emit(Mono.Cecil.Cil.OpCodes.Stloc, eventContext);

            //InvokeEventMethod
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldsfld, aspectField);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, eventContext);
            il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, InvokeEventMethod);

            //Restore original invoke event parameters
            //locals.Reverse();
            foreach (var local in locals)
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, local);
        }
    }
}
