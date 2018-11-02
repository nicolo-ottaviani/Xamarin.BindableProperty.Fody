using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using Fody;
using System;
using System.Xml.Linq;
using System.Xml;
using System.Xml.XPath;
using System.IO;

public class ModuleWeaver: BaseModuleWeaver
{
    
    public override void Execute()
    {
        try
        {
            var mscorlibPath = References.Split(';').FirstOrDefault(x => Path.GetFileName(x).ToLower() == "mscorlib.dll");
            var xamarinFormsCorePath = References.Split(';').FirstOrDefault(x => Path.GetFileName(x).ToLower() == "Xamarin.Forms.Core.dll".ToLower());

            if (mscorlibPath == null) throw new Exception($"Cannot find mscorlib.dll among the project references. \n Project references: \n{References}");
            if (xamarinFormsCorePath == null) throw new Exception($"Cannot find Xamarin.Forms.Core.dll among the project references. \n Project references: \n{References}");

            var module = ModuleDefinition;
            var mscorlibModule = ModuleDefinition.ReadModule(
                mscorlibPath,
                new ReaderParameters() { AssemblyResolver = module.AssemblyResolver }
                );
            var xamarinFormsCoreModule = ModuleDefinition.ReadModule(
                xamarinFormsCorePath,
                new ReaderParameters() { AssemblyResolver = module.AssemblyResolver }
                );

            //load some types and methods
            var systemTypeType = module.ImportReference(new TypeReference("System", "Type", mscorlibModule, mscorlibModule).Resolve());
            var systemStringType = module.ImportReference(new TypeReference("System", "String", mscorlibModule, mscorlibModule).Resolve());
            var systemObjectType = module.ImportReference(new TypeReference("System", "Object", mscorlibModule, mscorlibModule).Resolve());
            var runtimeTypeHandleType = module.ImportReference(new TypeReference("System", "RuntimeTypeHandle", mscorlibModule, mscorlibModule));
            var bindableObjectType = module.ImportReference(new TypeReference("Xamarin.Forms", "BindableObject", xamarinFormsCoreModule, xamarinFormsCoreModule).Resolve());
            var bindablePropertyType = module.ImportReference(new TypeReference("Xamarin.Forms", "BindableProperty", xamarinFormsCoreModule, xamarinFormsCoreModule).Resolve());
            var bindingModeType = module.ImportReference(new TypeReference("Xamarin.Forms", "BindingMode", xamarinFormsCoreModule, xamarinFormsCoreModule).Resolve());
            var bindablePropertyTypeNestedTypes = bindablePropertyType.Resolve().NestedTypes;
            var validateValueDelegateType = module.ImportReference(bindablePropertyTypeNestedTypes.FirstOrDefault(x => x.Name == "ValidateValueDelegate"));
            var bindingPropertyChangedDelegateType = module.ImportReference(bindablePropertyTypeNestedTypes.FirstOrDefault(x => x.Name == "BindingPropertyChangedDelegate"));
            var bindingPropertyChangingDelegateType = module.ImportReference(bindablePropertyTypeNestedTypes.FirstOrDefault(x => x.Name == "BindingPropertyChangingDelegate"));
            var coerceValueDelegateType = module.ImportReference(bindablePropertyTypeNestedTypes.FirstOrDefault(x => x.Name == "CoerceValueDelegate"));
            var createDefaultValueDelegateType = module.ImportReference(bindablePropertyTypeNestedTypes.FirstOrDefault(x => x.Name == "CreateDefaultValueDelegate"));

            var getTypeFromHandleMethod =
                module.ImportReference(
                    new MethodReference("GetTypeFromHandle", systemTypeType, systemTypeType)
                    .WithParams(
                        new ParameterDefinition(runtimeTypeHandleType)
                    ).Resolve()
                );
            var bindablePropertyCreateMethod =
                module.ImportReference(
                    new MethodReference("Create", bindablePropertyType, bindablePropertyType)
                    .WithParams(
                        new ParameterDefinition(systemStringType),
                        new ParameterDefinition(systemTypeType),
                        new ParameterDefinition(systemTypeType),
                        new ParameterDefinition(systemObjectType),
                        new ParameterDefinition(bindingModeType),
                        new ParameterDefinition(validateValueDelegateType),
                        new ParameterDefinition(bindingPropertyChangedDelegateType),
                        new ParameterDefinition(bindingPropertyChangingDelegateType),
                        new ParameterDefinition(coerceValueDelegateType),
                        new ParameterDefinition(createDefaultValueDelegateType)
                    ).Resolve()
                );
            var getValueMethod = module.ImportReference(bindableObjectType.Resolve().Methods.FirstOrDefault(x => x.Name == "GetValue"));
            var setValueMethod = module.ImportReference(bindableObjectType.Resolve().Methods.FirstOrDefault(x => x.Name == "SetValue"));

            foreach (var type in module.Types.Where(x => x.DerivesFrom("Xamarin.Forms", "BindableObject", true)))
            {
                //var xxx_il = type.Methods.FirstOrDefault(x => x.Name.StartsWith("__On"))?.Body?.Instructions;
                foreach (var property in type.Properties.Where(x => x.CustomAttributes.Any(attr => attr.AttributeType.Name == "BindableAttribute")))
                {

                    //add the XxxProperty static field
                    var staticField = type.Fields.FirstOrDefault(x => x.Name == (property.Name + "Property"));
                    if (staticField == null)
                    {
                        staticField = new FieldDefinition(property.Name + "Property", FieldAttributes.Static | FieldAttributes.Public | FieldAttributes.InitOnly, bindablePropertyType);
                        type.Fields.Add(staticField);
                    }

                    var propertyType = module.ImportReference(property.PropertyType);

                    //check if a On_Xxx_Changed method exists and create a wrapper method __On_Xxx_Changed
                    var onChangedMethod = type.Methods.FirstOrDefault(x => x.Name == $"On{property.Name}Changed" && !x.IsStatic && x.Parameters.Count == 1 && x.Parameters[0].ParameterType.FullName == property.PropertyType.FullName);
                    MethodDefinition onChangedWrapperMethod = null;
                    if (onChangedMethod != null)
                    {
                        onChangedWrapperMethod =
                            new MethodDefinition($"__On{property.Name}Changed", MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void)
                            .WithParams(
                                new ParameterDefinition("x", 0, bindableObjectType),
                                new ParameterDefinition("o", 0, systemObjectType),
                                new ParameterDefinition("n", 0, systemObjectType)
                            );
                        type.Methods.Add(onChangedWrapperMethod);
                        onChangedWrapperMethod.Body.Instructions.Clear();
                        var il = onChangedWrapperMethod.Body.GetILProcessor();
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ldarg_0));
                        il.Append(Instruction.Create(OpCodes.Castclass, type));
                        il.Append(Instruction.Create(OpCodes.Ldarg_2));
                        il.Append(Instruction.Create(OpCodes.Castclass, module.ImportReference(property.PropertyType)));
                        il.Append(Instruction.Create(OpCodes.Callvirt, onChangedMethod));
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ret));
                    }

                    //add the initialization of XxxProperty field in the static constructor
                    var staticCctorAttributes = MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static;
                    var staticCctor = type.Methods.FirstOrDefault(x => x.Name == ".cctor" && x.Attributes.HasFlag(staticCctorAttributes));
                    if (staticCctor == null)
                    {
                        staticCctor = new MethodDefinition(".cctor", MethodAttributes.Private | staticCctorAttributes, module.TypeSystem.Void);
                        type.Methods.Add(staticCctor);
                    }
                    type.IsBeforeFieldInit = false;
                    if (!staticCctor.Body.Instructions.Any(x => x.OpCode == OpCodes.Stsfld && x.Operand is FieldDefinition && (x.Operand as FieldDefinition).Name == (property.Name + "Property")))
                    {
                        var il = staticCctor.Body.GetILProcessor();
                        if (staticCctor.Body.Instructions.Count > 0 && staticCctor.Body.Instructions.Last().OpCode == OpCodes.Ret)
                            il.Remove(staticCctor.Body.Instructions.Last());
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ldstr, property.Name));
                        il.Append(Instruction.Create(OpCodes.Ldtoken, module.ImportReference(property.PropertyType)));
                        il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                        il.Append(Instruction.Create(OpCodes.Ldtoken, type));
                        il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                        getDefaultValue(module, property, il);
                        il.Append(Instruction.Create(OpCodes.Ldc_I4_1));
                        il.Append(Instruction.Create(OpCodes.Ldnull));
                        if (onChangedMethod != null)
                        {
                            il.Append(Instruction.Create(OpCodes.Ldnull));
                            il.Append(Instruction.Create(OpCodes.Ldftn, onChangedWrapperMethod));
                            il.Append(Instruction.Create(OpCodes.Newobj, module.ImportReference(bindingPropertyChangedDelegateType.Resolve().Methods.FirstOrDefault(x => x.Name == ".ctor"))));
                        }
                        else
                        {
                            il.Append(Instruction.Create(OpCodes.Ldnull));
                        }
                        il.Append(Instruction.Create(OpCodes.Ldnull));
                        il.Append(Instruction.Create(OpCodes.Ldnull));
                        il.Append(Instruction.Create(OpCodes.Ldnull));
                        il.Append(Instruction.Create(OpCodes.Call, bindablePropertyCreateMethod));
                        il.Append(Instruction.Create(OpCodes.Stsfld, staticField));
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ret));
                    }

                    //change the getter of the bindable property
                    {
                        var il = property.GetMethod.Body.GetILProcessor();
                        il.Body.Instructions.Clear();
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ldarg_0));
                        il.Append(Instruction.Create(OpCodes.Ldsfld, staticField));
                        il.Append(Instruction.Create(OpCodes.Call, getValueMethod));
                        if (propertyType.IsValueType)
                        {
                            il.Body.InitLocals = true;
                            il.Body.Variables.Add(new VariableDefinition(propertyType));
                            il.Append(Instruction.Create(OpCodes.Unbox_Any, propertyType));
                            il.Append(Instruction.Create(OpCodes.Stloc_0));
                            il.Append(Instruction.Create(OpCodes.Ldloc_0));
                        }
                        else
                        {
                            il.Append(Instruction.Create(OpCodes.Castclass, propertyType));
                        }
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ret));
                        var attr = property.GetMethod.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
                        if (attr != null) property.GetMethod.CustomAttributes.Remove(attr);
                    }

                    //change the setter of the bindable property
                    if (!property.SetMethod.Body.Instructions.Any(x => x.OpCode == OpCodes.Call && x.Operand is MethodReference && (x.Operand as MethodReference).FullName == setValueMethod.FullName))
                    {
                        var il = property.SetMethod.Body.GetILProcessor();
                        il.Body.Instructions.Clear();
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ldarg_0));
                        il.Append(Instruction.Create(OpCodes.Ldsfld, staticField));
                        il.Append(Instruction.Create(OpCodes.Ldarg_1));
                        if (propertyType.IsValueType)
                            il.Append(Instruction.Create(OpCodes.Box, propertyType));
                        il.Append(Instruction.Create(OpCodes.Call, setValueMethod));
                        il.Append(Instruction.Create(OpCodes.Nop));
                        il.Append(Instruction.Create(OpCodes.Ret));
                        var attr = property.SetMethod.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
                        if (attr != null) property.SetMethod.CustomAttributes.Remove(attr);
                    }

                    //remove the BindableAttribute
                    property.CustomAttributes.Remove(property.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.Name == "BindableAttribute"));

                    LogInfo($"Property '{property.PropertyType.Name} {type.Name}.{property.Name}' is now a bindable property");
                    if (onChangedMethod != null) LogInfo($"    (with associated {onChangedMethod.Name})");
                }
            }
            //module.Write();
        }
        catch (Exception exc)
        {
            LogError($"^^^ {typeof(ModuleWeaver).Assembly.GetName().Name}  --ERROR: {exc.Message} \r\n{exc.StackTrace}");
            return;
        }
        LogInfo($"^^^ {typeof(ModuleWeaver).Assembly.GetName().Name} ended ^^^");
    }

    private void getDefaultValue(ModuleDefinition module, PropertyDefinition property, ILProcessor il)
    {
        //foreach (var instr in getDefaultValue(module.ImportReference(property.PropertyType)))
        //    il.Append(instr);
        var propertyType = module.ImportReference(property.PropertyType);
        if (!propertyType.IsValueType)
            il.Append(Instruction.Create(OpCodes.Ldnull));
        else if (propertyType.FullName == "System.Byte")
        {
            il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
            il.Append(Instruction.Create(OpCodes.Conv_U1));
        }
        else if (propertyType.FullName == "System.Int16" || propertyType.FullName == "System.UInt16")
        {
            il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
            il.Append(Instruction.Create(OpCodes.Conv_U2));
        }
        else if (propertyType.FullName == "System.Int32" || propertyType.FullName == "System.UInt32")
            il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
        else if (propertyType.FullName == "System.Int64" || propertyType.FullName == "System.UInt64")
        {
            il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
            il.Append(Instruction.Create(OpCodes.Conv_I8));
        }
        else if (propertyType.FullName == "System.Single")
            il.Append(Instruction.Create(OpCodes.Ldc_R4, 0f));
        else if (propertyType.FullName == "System.Double")
            il.Append(Instruction.Create(OpCodes.Ldc_R8, 0d));
        else
        {
            il.Body.InitLocals = true;
            var localVar = new VariableDefinition(propertyType);
            il.Body.Variables.Add(localVar);
            il.Append(Instruction.Create(OpCodes.Ldloca_S, localVar));
            il.Append(Instruction.Create(OpCodes.Initobj, propertyType));
            il.Append(Instruction.Create(OpCodes.Ldloc_0));
        }
        il.Append(Instruction.Create(OpCodes.Box, propertyType));
    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        yield return "netstandard";
        yield return "mscorlib";
    }

    public override bool ShouldCleanReference => true;
}

enum Platform
{
    Unknown = 0,
    Android,
    iOS,
}