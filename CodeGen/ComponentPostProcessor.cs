using Gil.Authoring.Components;
using Gil.Authoring.Editor;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Gil.Authoring.CodeGen {

  class ComponentPostProcessor {

    /// <summary>
    /// Holds Assembly filtered types
    /// </summary>
    struct AssemblyTypes {

      public AssemblyTypes(AssemblyDefinition assemblyDef) {
        Assembly = assemblyDef;
        TypeMap = assemblyDef.Modules.SelectMany(m => m.Types.SelectMany(t => new[] { t }.Union(t.NestedTypes)))
          .ToDictionary(t => t.GetUniqueName(), t => t);
        TypeNames = TypeMap.Values.Select(t => t.GetUniqueName()).ToList();
        BaseTypeRefs = TypeMap.Values.SelectMany(t => t.GetBaseTypes()).ToList();
      }

      public TypeDefinition GetType(string hint) =>
        TypeMap.TryGetValue(TypeMap.Keys.FirstOrDefault(k => k.StartsWith(hint)) ?? "N/A", out var type) ? type : default;

      public readonly AssemblyDefinition Assembly;
      public readonly IDictionary<string, TypeDefinition> TypeMap;
      public readonly IEnumerable<string> TypeNames;
      public readonly IEnumerable<TypeReference> BaseTypeRefs;
    }

    static readonly string EditorInitKey = $"{typeof(ComponentPostProcessor).FullName}.{nameof(EditorInitKey)}";
    static List<AssemblyDefinition> m_assemblies = new();

    /// <summary>
    /// Preprocessing entry point called by the editor every time scripts are compiled
    /// </summary>
    [InitializeOnLoadMethod()]
    static void OnEditorLoad() {
      CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
      CompilationPipeline.compilationFinished += OnCompilationFinished;

      // We must request the scripts to be compiled the first time the editor is loaded
      if (!SessionState.GetBool(EditorInitKey, false)) {
        CompilationPipeline.RequestScriptCompilation();
        SessionState.SetBool(EditorInitKey, true);
        Debug.Log("Initialized compilation pipeline");
      }
    }

    /// <summary>
    /// Called for each assembly that was changed and will be recompiled
    /// </summary>
    /// <param name="assemblyPath"></param>
    /// <param name="msg"></param>
    static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] msg) {
      PostProcessAssembly(assemblyPath);
    }

    /// <summary>
    /// Called when compilation is finished
    /// </summary>
    /// <param name="obj"></param>
    static void OnCompilationFinished(object obj) {
      foreach (var assembly in m_assemblies)
        assembly.Write(new WriterParameters());
      m_assemblies.Clear();
    }

    /// <summary>
    /// Post process an assembly from a path
    /// </summary>
    /// <param name="path"></param>
    static void PostProcessAssembly(string path) {
      // Skips this assembly
      if (path.Contains("Gil.Authoring"))
        return;

      var assemblyResolver = new AssemblyResolver(Enumerable.Empty<string>(), true);
      var targetAssembly = assemblyResolver.AddAssembly(path, true);

      Debug.Log($"About to post process assembly {targetAssembly.Name}");
      var assemblyTypes = new AssemblyTypes(targetAssembly);

      var typesToInject = new Dictionary<string, IEnumerable<TypeDefinition>>() {
        //["Authoring Types"] = GetTypesWithAttribute(assemblyTypes, typeof(GenerateAuthoringAttribute))
        //                      .Select(t => CreateAuthoringComponent(t, assemblyTypes.GetType("Procedural.PlaceholderComponent"))).ToList(),
        ["Editors"] = InjectInpectorEditors(assemblyTypes),
        ["Drawer"] = InjectDrawers(assemblyTypes),
        ["Bakers"] = InjectComponentBakers(assemblyTypes)
      };

      var postProcessLog = typesToInject.Where(kvp => kvp.Value.Any())
        .Select(kvp => $"--==Injecting {kvp.Key}==--\n{kvp.Value.Select(t => t?.FullName).Aggregate(string.Empty, (acc, t) => $"{acc}Injecting type {t}\n")}\n\n")
        .Aggregate(string.Empty, (acc, v) => $"{acc}{v}");
      Debug.Log(postProcessLog);

      var newAssemblyTypes = new AssemblyTypes(targetAssembly);
      var diffAdded = newAssemblyTypes.TypeNames
        .Except(assemblyTypes.TypeNames).ToList();
      var diffRemoved = assemblyTypes.TypeNames
        .Except(newAssemblyTypes.TypeNames).ToList();
      Debug.Log(diffAdded.Aggregate("--== Assembly Diff Added ==--\n", (acc, t) => $"{acc}{t}\n"));
      Debug.Log(diffRemoved.Aggregate("--== Assembly Diff Removed ==--\n", (acc, t) => $"{acc}{t}\n"));

      // Only write assembly if there was any type change
      if (typesToInject.Values.Any(v => v.Any()))
        m_assemblies.Add(targetAssembly);
    }

    /// <summary>
    /// Inject Editors for all types deriving from GenericComponentAuthoring
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectInpectorEditors(AssemblyTypes assemblyTypes) {
      var injectedEditors = InjectTypes(assemblyTypes, "AuthoringEditor", typeof(GenericComponentAuthoring), typeof(GenericInspectorEditor<>));
      var customEditorAttributeType = assemblyTypes.Assembly.MainModule.ImportReference(typeof(CustomEditor));
      foreach (var editor in injectedEditors) {
        var inspectedType = (editor.BaseType as GenericInstanceType).GenericArguments.First();
        var attr = CecilUtility.CreateCustomAttribute(customEditorAttributeType, inspectedType);
        editor.CustomAttributes.Add(attr);
      }
      return injectedEditors;
    }

    static IEnumerable<TypeDefinition> InjectDrawers(AssemblyTypes assemblyTypes) {
      var injectedTypes = InjectTypes(assemblyTypes, "CustomDrawer", typeof(IBufferElementData), typeof(GenericDrawer<>));
      var attributeType = assemblyTypes.Assembly.MainModule.ImportReference(typeof(CustomPropertyDrawer));
      foreach (var type in injectedTypes) {
        var inspectedType = (type.BaseType as GenericInstanceType).GenericArguments.First();
        var attr = CecilUtility.CreateCustomAttribute(attributeType, inspectedType);
        type.CustomAttributes.Add(attr);
      }
      return injectedTypes;
    }

    /// <summary>
    /// Inject Bakers for all GenericComponentAuthoring
    /// </summary>
    /// <param name="assemblyTypes"></param>
    /// <returns></returns>
    static IEnumerable<TypeDefinition> InjectComponentBakers(AssemblyTypes assemblyTypes) =>
      InjectTypes(assemblyTypes, "Baker", typeof(GenericComponentAuthoring), typeof(GenericBaker<>), typeof(Baker<>));

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
    static IEnumerable<TypeDefinition> InjectTypes(AssemblyTypes assemblyTypes, string newTypeName, Type searchBaseType, Type baseType, params Type[] otherTypes) {

      var bakers = otherTypes.Append(baseType);

      // 1. Find All types that derive from searchBaseType

      var foundTypes = assemblyTypes.TypeMap.Values.Where(t => searchBaseType.IsAssignableFrom(t));

      if (!foundTypes.Any())
        return Enumerable.Empty<TypeDefinition>();

      // 2. From those found types get all that are used and not used as generic parameter of the baseType

      var usedTypes = assemblyTypes.BaseTypeRefs.Where(t => t.IsGenericInstance)
        .Select(t => t as GenericInstanceType)
        .Where(t => bakers.Any(b => t.ElementType.EqualsToType(b)) && t.GenericArguments.Count == 1)
        .Select(t => t.GenericArguments.First());

      var unusedTypes = foundTypes.Except(usedTypes);

      // 3. Create a class extending from baseType and with generic parameter the unusedType (eg. `class newType : baseType<unusedType> { })

      var baseTypeRef = assemblyTypes.Assembly.MainModule.ImportReference(baseType);
      var injectedTypes = new List<TypeDefinition>();
      foreach (var t in unusedTypes) {
        var genericInstance = new GenericInstanceType(baseTypeRef);
        genericInstance.GenericArguments.Add(t);
        var newType = CecilUtility.CreateTypeWithDefaultConstructor(string.Empty, $"{t.Name}{newTypeName}", genericInstance);
        injectedTypes.Add(newType);
        assemblyTypes.TypeMap[t.GetUniqueName()].NestedTypes
          .Add(newType);
      }
      return injectedTypes;
    }

    static IEnumerable<TypeDefinition> GetTypesWithAttribute(AssemblyTypes assemblyTypes, Type attribute) =>
      assemblyTypes.TypeMap.Values
        .Where(t => t.CustomAttributes.Any(attr => attr.AttributeType.EqualsToType(attribute)));
  }
}