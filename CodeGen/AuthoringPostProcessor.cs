using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gil.Authoring.Components;
using Gil.Authoring.Editor;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Entities;
using UnityEditor;

//[assembly: InternalsVisibleTo("Gil.Authoring.Tests")]
namespace Gil.Authoring.CodeGen {

  internal class AuthoringPostProcessor : ILPostProcessor {

    static string[] IgnoredAssemblies = new[] {
      "CodeGen",
      "Gil.Authoring",
      "Unity",
      "UniRx"
    };

    public override ILPostProcessor GetInstance() {
      return this;
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly) {
      return !IgnoredAssemblies.Any(hint => compiledAssembly.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly) {
      var diagnostics = new List<DiagnosticMessage>();
      if (!WillProcess(compiledAssembly))
        return new ILPostProcessResult(null, diagnostics);

      var assemblyDefinition = PostProcessAssembly(compiledAssembly);

      var result = CreatePostProcessResult(assemblyDefinition, diagnostics);
      return result;
    }

    /// <summary>
    /// Post process an assembly from a compiled assembly
    /// </summary>
    /// <param name="path"></param>
    static AssemblyDefinition PostProcessAssembly(ICompiledAssembly assembly) =>
      ILComponentPostProcessor.PostProcessAssembly(
        assembly.Name,
        assembly.InMemoryAssembly.PeData,
        assembly.InMemoryAssembly.PdbData,
        assembly.References);

    static ILPostProcessResult CreatePostProcessResult(AssemblyDefinition assembly, List<DiagnosticMessage> diagnostics) {
      using var pe = new MemoryStream();
      using var pdb = new MemoryStream(); var writerParameters = new WriterParameters {
        WriteSymbols = true,
        SymbolStream = pdb,
        SymbolWriterProvider = new PortablePdbWriterProvider()
      };

      assembly.Write(pe, writerParameters);
      pe.Flush();
      pdb.Flush();
      return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
    }
  }

  internal static class ILComponentPostProcessor {

    /// <summary>
    /// Holds Assembly filtered types fo fast access
    /// </summary>
    struct AssemblyTypes {

      public AssemblyTypes(AssemblyDefinition assemblyDef) {
        Assembly = assemblyDef;
        TypeMap = assemblyDef.Modules.SelectMany(m => m.Types.SelectMany(t => new[] { t }.Union(t.NestedTypes)))
          // TODO: Unique name should never have duplicates for the same assembly
          .GroupBy(t => t.GetUniqueName())
          .ToDictionary(t => t.Key, t => t.First());
        TypeNames = TypeMap.Values.Select(t => t.GetUniqueName()).ToList();
        BaseTypeRefs = TypeMap.Values.SelectMany(t => t.GetBaseTypes()).ToList();
      }

      public void AddType(TypeDefinition type) =>
        TypeMap[type.GetUniqueName(Assembly.MainModule)] = type;

      public TypeDefinition GetType(TypeReference type) =>
        TypeMap.TryGetValue(type.GetUniqueName(), out var typeDef) ? typeDef : default;

      public TypeDefinition GetType(string hint) =>
        Types.FirstOrDefault(t => t.FullName.StartsWith(hint));

      public readonly AssemblyDefinition Assembly;
      public readonly IEnumerable<TypeDefinition> Types { get => TypeMap.Values; }
      public readonly IEnumerable<string> TypeNames;
      public readonly IEnumerable<TypeReference> BaseTypeRefs;
      readonly IDictionary<string, TypeDefinition> TypeMap;
    }

    static List<string> ProcessLog = new();

    /// <summary>
    /// Post process an assembly from an assembly data
    /// </summary>
    internal static AssemblyDefinition PostProcessAssembly(string assemblyName, byte[] assemblyData, byte[] symbolData, string[] references) {
      using var assemblyResolver = new AssemblyResolver(references, true);
      var assembly = assemblyResolver.AddAssembly(assemblyName, assemblyData, symbolData, false);
      PostProcessAssembly(assembly);
      return assembly;
    }

    /// <summary>
    /// Post process an assembly from an assembly definition
    /// </summary>
    internal static void PostProcessAssembly(AssemblyDefinition targetAssembly) {
      ProcessLog.Add($"{DateTime.Now} - Processing assembly {targetAssembly.Name}");
      var assemblyTypes = new AssemblyTypes(targetAssembly);

      var typesToInject = new Dictionary<string, IEnumerable<TypeDefinition>>() {
        ["Authoring"] = InjectAuthoringMembers(assemblyTypes),
        ["Drawer"] = InjectDrawers(assemblyTypes),
        ["Bakers"] = InjectComponentBakers(assemblyTypes)
      };

      ProcessLog.Add(
        typesToInject.Where(kvp => kvp.Value.Any())
          .Select(kvp => $"--==Injecting {kvp.Key}==--\n{kvp.Value.Select(t => t?.FullName).Aggregate(string.Empty, (acc, t) => $"{acc}Injecting type {t}\n")}\n\n")
          .Aggregate(string.Empty, (acc, v) => $"{acc}{v}"));

      var newAssemblyTypes = new AssemblyTypes(targetAssembly);
      var diffAdded = newAssemblyTypes.TypeNames
        .Except(assemblyTypes.TypeNames).ToList();
      var diffRemoved = assemblyTypes.TypeNames
        .Except(newAssemblyTypes.TypeNames).ToList();
      ProcessLog.Add(diffAdded.Aggregate("--== Assembly Diff Added ==--\n", (acc, t) => $"{acc}{t}\n"));
      ProcessLog.Add(diffRemoved.Aggregate("--== Assembly Diff Removed ==--\n", (acc, t) => $"{acc}{t}\n"));

      ProcessLog.Add("\n** Assembly References **");
      ProcessLog.AddRange(targetAssembly.MainModule.AssemblyReferences.Select(r => r.FullName));

      WriteLog(targetAssembly);
    }

    /// <summary>
    /// Writes the output log of the post process
    /// </summary>
    /// <param name="targetAssembly"></param>
    static void WriteLog(AssemblyDefinition targetAssembly) {
      File.WriteAllLines("Logs/PostProcess.log", ProcessLog);
    }

    /// <summary>
    /// Inject property drawers for all IComponentData and IBufferElementData
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectDrawers(AssemblyTypes assemblyTypes) {
      var injectedTypes = InjectTypes(assemblyTypes, "CustomDrawer", new[] { typeof(IComponentData), typeof(IBufferElementData) }, typeof(GenericDrawer<>));
      var propertyDrawerAttributeType = assemblyTypes.Assembly.MainModule.ImportReference(typeof(CustomPropertyDrawer));
      foreach (var type in injectedTypes) {
        var inspectedType = (type.BaseType as GenericInstanceType).GenericArguments.First();
        ProcessLog.Add($"- Custom Attribute {type} - {propertyDrawerAttributeType}' {inspectedType}");
        type.CustomAttributes.Add(CecilUtility.CreateCustomAttribute(propertyDrawerAttributeType, inspectedType));
      }
      return injectedTypes;
    }

    /// <summary>
    /// Inject Bakers for all GenericComponentAuthoring
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectComponentBakers(AssemblyTypes assemblyTypes) =>
       InjectTypes(assemblyTypes, "Baker", typeof(IGenericComponentAuthoring), typeof(GenericBaker<>), typeof(Baker<>));

    /// <summary>
    /// Inject Authoring fields from type `GenericComponentAuthoring` generic arguments
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectAuthoringMembers(AssemblyTypes assemblyTypes) {
      var genericComponentAuthoring = assemblyTypes.Assembly.MainModule.ImportReference(typeof(GenericComponentAuthoring<>)).Resolve();
      var authoringComponents = assemblyTypes.Types
        .Select(t => (Component: t, BaseGenericInstanceType: CecilUtility.FindBaseTypeInHierarchy(t, genericComponentAuthoring) as GenericInstanceType))
        .Where(r => r.BaseGenericInstanceType != default);

      ProcessLog.Add("\n**Found authoring types**");
      ProcessLog.AddRange(authoringComponents.Select(c => $"{c.Component.FullName} : {c.BaseGenericInstanceType.FullName}"));

      // Local helper method to create a fields only in this context
      static FieldDefinition CreateField(string name, TypeReference fieldType, TypeDefinition fieldTypeDef) {
        var contraintType = BakerConstraints.PossibleInterfaces
          .FirstOrDefault(i => {
            var reference = fieldType.Module.ImportReference(i.Type);
            var resolvedType = (reference is GenericInstanceType gt) ? gt.GenericArguments.First() : reference;
            return CecilUtility.IsAssignableFrom(fieldType, resolvedType);
          }).Type;
        // if the field belongs to this assembly make it serializable
        if (fieldTypeDef != null)
          fieldTypeDef.Attributes |= TypeAttributes.Serializable;

        var isArray = contraintType != null &&
          CecilUtility.IsAssignableFrom(fieldType.Module.ImportReference(contraintType), fieldType.Module.ImportReference(typeof(IEnumerable)));
        fieldType = isArray ? new ArrayType(fieldType) : fieldType;
        return new FieldDefinition(name, FieldAttributes.Public, fieldType);
      }

      var fields = authoringComponents.SelectMany(a =>
        a.BaseGenericInstanceType.GenericArguments
          .GroupBy(k => k)
          .SelectMany(group =>
            group.Select((type, idx) =>
            (a.Component, Field: CreateField($"Value{(group.Count() > 1 ? ++idx : string.Empty)}", group.Key, assemblyTypes.GetType(group.Key))))));

      foreach (var (component, field) in fields) {
        component.Fields.Add(field);
        ProcessLog.Add($"- Added {field.FullName} to authoring component {component.Name}");
      }

      return authoringComponents.Select(a => a.Component);
    }

    /// <summary>
    /// Search for base types matching the searchBaseType and for each one of them add new nested type
    /// deriving from baseType<foundType> if no type yet derives from it
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <param name="newTypeName"></param>
    /// <param name="searchBaseType"></param>
    /// <param name="baseType"></param>
    /// <param name="otherTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectTypes(AssemblyTypes assemblyTypes, string newTypeName, Type searchBaseType, Type baseType, params Type[] otherTypes) =>
      InjectTypes(assemblyTypes, newTypeName, new[] { searchBaseType }, baseType, otherTypes);

    /// <summary>
    /// Search for base types matching the searchBaseType and for each one of them add new nested type
    /// deriving from baseType<foundType> if no type yet derives from it
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <param name="newTypeName"></param>
    /// <param name="searchBaseTypes"></param>
    /// <param name="baseType"></param>
    /// <param name="otherTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectTypes(AssemblyTypes assemblyTypes, string newTypeName, Type[] searchBaseTypes, Type baseType, params Type[] otherTypes) {

      // 1. Find All types that derive from searchBaseType

      ProcessLog.Add($"\n--== Injecting {newTypeName} ==--");
      ProcessLog.Add("**Searching for types deriving from**");
      ProcessLog.AddRange(searchBaseTypes.Select(t => t.FullName));

      var foundTypes = assemblyTypes.Types.Where(t => searchBaseTypes.Any(st => st.IsAssignableFrom(t)));

      ProcessLog.Add("\n**Found**");
      ProcessLog.AddRange(foundTypes.Select(t => t.FullName));

      if (!foundTypes.Any())
        return Enumerable.Empty<TypeDefinition>();

      // 2. From those found types get all that are used and not used as generic parameter of the baseType

      var possibleBaseTypes = otherTypes.Append(baseType);
      var usedTypes = assemblyTypes.BaseTypeRefs.Where(t => t.IsGenericInstance)
        .Select(t => t as GenericInstanceType)
        .Where(t => possibleBaseTypes.Any(b => t.ElementType.EqualsToType(b)) && t.GenericArguments.Count == 1)
        .Select(t => t.GenericArguments.First());

      var unusedTypes = foundTypes.Except(usedTypes).ToList();

      ProcessLog.Add($"\n**Types that need injection on {newTypeName}**");
      ProcessLog.AddRange(unusedTypes.Select(t => t.FullName));

      // 3. Create a class extending from baseType and with generic parameter the unusedType (eg. `class newType : baseType<unusedType> { })

      var baseTypeRef = assemblyTypes.Assembly.MainModule.ImportReference(baseType);
      var injectedTypes = new List<TypeDefinition>();
      foreach (var t in unusedTypes) {
        var genericInstance = new GenericInstanceType(baseTypeRef);
        genericInstance.GenericArguments.Add(t);
        var newType = CecilUtility.CreateTypeWithDefaultConstructor(string.Empty, $"{t.Name}{newTypeName}", genericInstance.Module, genericInstance);
        newType.Attributes |= TypeAttributes.NestedPublic;
        injectedTypes.Add(newType);
        assemblyTypes.GetType(t).NestedTypes
          .Add(newType);
        assemblyTypes.AddType(newType);
      }
      return injectedTypes;
    }
  }
}