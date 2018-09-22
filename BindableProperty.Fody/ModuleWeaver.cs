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

public class ModuleWeaver: BaseModuleWeaver
{

    static readonly string monoAndroidDirpath =
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2017\\Community\\Common7\\IDE\\ReferenceAssemblies\\Microsoft\\Framework\\MonoAndroid\\v1.0";
    static readonly string xamarinIosDirpath =
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Reference Assemblies\\Microsoft\\Framework\\Xamarin.iOS\\v1.0";


    public override void Execute()
    {
        try
        {
            var csprojFilepath = System.IO.Directory.GetFiles(ProjectDirectoryPath, "*.csproj").First();

            extractInfoFromCsproj(csprojFilepath, out var xamarinFormsVersion, out var assemblyName, out var platform);

            if (xamarinFormsVersion == null) throw new FormatException($"Cannot detect Xamarin.Forms version from the .csproj file");
            if (assemblyName == null) throw new FormatException($"Cannot detect assembly name from the .csproj file");
            if (platform == Platform.Unknown) throw new FormatException($"Cannot detect the platform (Droid or iOS) from the .csproj file");

            var xamarinFormsDirpath =
                $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\.nuget\\packages\\xamarin.forms\\{xamarinFormsVersion}\\lib\\MonoAndroid10";
            if (!System.IO.Directory.Exists(xamarinFormsDirpath)) throw new System.IO.FileNotFoundException($"Cannot find folder {xamarinFormsDirpath}");

            //var dllFilepath = $"{System.IO.Path.GetDirectoryName(csprojFilepath)}\\{outDirpath.TrimEnd('\\')}\\{assemblyName}.dll";
            //if (!System.IO.File.Exists(dllFilepath)) throw new System.IO.FileNotFoundException($"Cannot find file {dllFilepath}");




            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(xamarinFormsDirpath);
            resolver.AddSearchDirectory(platform == Platform.Android ? monoAndroidDirpath : xamarinIosDirpath);

            //var module = ModuleDefinition.ReadModule(
            //    System.IO.File.Open(dllFilepath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite),
            //    new ReaderParameters() { AssemblyResolver = resolver }
            //    );
            LogInfo($"Reference = {this.References}");
            var module = ModuleDefinition;
            var mscorlibModule = ModuleDefinition.ReadModule(
                (platform == Platform.Android ? monoAndroidDirpath : xamarinIosDirpath) + "\\mscorlib.dll",
                new ReaderParameters() { AssemblyResolver = resolver }
                );
            var xamarinFormsCoreModule = ModuleDefinition.ReadModule(
                xamarinFormsDirpath + "\\Xamarin.Forms.Core.dll",
                new ReaderParameters() { AssemblyResolver = resolver }
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
                        foreach (var instr in getDefaultValue(module.ImportReference(property.PropertyType)))
                            il.Append(instr);
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
                        il.Append(Instruction.Create(OpCodes.Castclass, property.PropertyType));
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
        //System.Threading.Thread.Sleep(2000);
        //var ns = GetNamespace();
        //var newType = new TypeDefinition(ns, "Hello", TypeAttributes.Public, TypeSystem.ObjectReference);
        //AddConstructor(newType);
        //AddHelloWorld(newType);
        //ModuleDefinition.Types.Add(newType);
        //LogInfo("Added type 'Hello' with method 'World'.");
    }

    static void extractInfoFromCsproj(string csprojFilepath, out string xamarinFormsVersion, out string assemblyName, out Platform platform)
    {
        var csproj = XDocument.Load(csprojFilepath);
        var man = new XmlNamespaceManager(csproj.CreateNavigator().NameTable);
        man.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");
        xamarinFormsVersion = csproj.XPathSelectAttribute("//x:PackageReference[@Include='Xamarin.Forms']/@Version", man)?.Value;
        assemblyName = csproj.XPathSelectElement("//x:AssemblyName", man)?.Value?.ToLower();
        if (assemblyName != null)
            platform = assemblyName.EndsWith("droid") ? Platform.Android : (assemblyName.EndsWith("ios") ? Platform.iOS : Platform.Unknown);
        else
            platform = Platform.Unknown;
    }

    static IEnumerable<Instruction> getDefaultValue(TypeReference propertyType)
    {
        if (!propertyType.IsValueType)
            yield return Instruction.Create(OpCodes.Ldnull);
        if (propertyType.FullName == "System.Byte")
        {
            yield return Instruction.Create(OpCodes.Ldc_I4_0);
            yield return Instruction.Create(OpCodes.Conv_U1);
        }
        if (propertyType.FullName == "System.Int16" || propertyType.FullName == "System.UInt16")
        {
            yield return Instruction.Create(OpCodes.Ldc_I4_0);
            yield return Instruction.Create(OpCodes.Conv_U2);
        }
        else if (propertyType.FullName == "System.Int32" || propertyType.FullName == "System.UInt32")
            yield return Instruction.Create(OpCodes.Ldc_I4_0);
        else if (propertyType.FullName == "System.Int64" || propertyType.FullName == "System.UInt64")
        {
            yield return Instruction.Create(OpCodes.Ldc_I4_0);
            yield return Instruction.Create(OpCodes.Conv_I8);
        }
        else if (propertyType.FullName == "System.Single")
            yield return Instruction.Create(OpCodes.Ldc_R4, 0f);
        else if (propertyType.FullName == "System.Double")
            yield return Instruction.Create(OpCodes.Ldc_R8, 0d);
        yield return Instruction.Create(OpCodes.Box, propertyType);
        
    }


    public override IEnumerable<string> GetAssembliesForScanning()
    {
        yield return "netstandard";
        yield return "mscorlib";
    }

    //string GetNamespace()
    //{
    //    var attributes = ModuleDefinition.Assembly.CustomAttributes;
    //    var namespaceAttribute = attributes.FirstOrDefault(x => x.AttributeType.FullName == "NamespaceAttribute");
    //    if (namespaceAttribute == null)
    //    {
    //        return null;
    //    }
    //    attributes.Remove(namespaceAttribute);
    //    return (string) namespaceAttribute.ConstructorArguments.First().Value;
    //}

    //void AddConstructor(TypeDefinition newType)
    //{
    //    var method = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, TypeSystem.VoidReference);
    //    var objectConstructor = ModuleDefinition.ImportReference(TypeSystem.ObjectDefinition.GetConstructors().First());
    //    var processor = method.Body.GetILProcessor();
    //    processor.Emit(OpCodes.Ldarg_0);
    //    processor.Emit(OpCodes.Call, objectConstructor);
    //    processor.Emit(OpCodes.Ret);
    //    newType.Methods.Add(method);
    //}

    //void AddHelloWorld(TypeDefinition newType)
    //{
    //    var method = new MethodDefinition("World", MethodAttributes.Public, TypeSystem.StringReference);
    //    var processor = method.Body.GetILProcessor();
    //    processor.Emit(OpCodes.Ldstr, "Hello World");
    //    processor.Emit(OpCodes.Ret);
    //    newType.Methods.Add(method);
    //}

    public override bool ShouldCleanReference => true;
}

enum Platform
{
    Unknown = 0,
    Android,
    iOS,
}