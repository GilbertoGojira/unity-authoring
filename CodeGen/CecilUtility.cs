using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Assertions;

namespace Gil.Authoring.CodeGen {

  public static class CecilUtility {

    public static IEnumerable<string> GetAssemblyPaths(IEnumerable<string> keywords, bool exclude = false) =>
      AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => keywords.Any(ex => (a.FullName.IndexOf(ex, StringComparison.InvariantCultureIgnoreCase) >= 0) != exclude ||
                                        a.ManifestModule.Name == ex != exclude))
                                        .Select(a => a.Location);

    public static TypeDefinition CreateTypeWithDefaultConstructor(string @namespace, string name, ModuleDefinition module, TypeReference baseType = default) {
      var newType = new TypeDefinition(
        @namespace,
        name,
        TypeAttributes.Class | TypeAttributes.Public,
        baseType ?? module.ImportReference(typeof(object)));

      var baseCtor = baseType?.Module.ImportReference(baseType.Resolve().GetConstructors().First());
      if (baseCtor != null)
        baseCtor.DeclaringType = baseType;

      // Add the constructor to the class
      newType.Methods.Add(CreateDefaultConstuctor(baseType.Module, baseCtor));

      return newType;
    }

    public static MethodDefinition CreateDefaultConstuctor(ModuleDefinition module, MethodReference baseCtor = default) {
      // Define the default constructor method (ctor) within the class
      var ctor = new MethodDefinition(
          ".ctor",                                // Name of the constructor
          MethodAttributes.Public |               // Specify the method attributes (e.g., Public)
          MethodAttributes.HideBySig |            // HideBySig is required for constructors
          MethodAttributes.SpecialName |          // SpecialName is required for constructors
          MethodAttributes.RTSpecialName,         // RTSpecialName is required for constructors
          module.ImportReference(typeof(void))     // Return type (void for constructors)
      );

      // Add the constructor's body (e.g., to call base constructor)
      // Here, we call the constructor of the base class (Object) using IL code
      var ilProcessor = ctor.Body.GetILProcessor();
      ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
      ilProcessor.Append(ilProcessor.Create(OpCodes.Call,
        baseCtor ?? module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes))));
      ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));

      return ctor;
    }

    /// <summary>
    /// Check if a method parameters match with the supplied ones
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static bool ParametersMatch(MethodDefinition method, TypeReference[] parameters) =>
      method.Parameters.Count == parameters.Count()
      && method.Parameters.Zip(parameters, (e1, e2) => {
        // TODO: Comparing type for some reason ends up using two different assemblies
        // We are just comparing the name 
        return e1.ParameterType.FullName == typeof(Type).FullName || e1.ParameterType.IsAssignableFrom(e2);
      })
        .All(v => v);

    /// <summary>
    /// Creates a custom attribute
    /// </summary>
    /// <param name="customEditorAttributeType"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static CustomAttribute CreateCustomAttribute(TypeReference customEditorAttributeType, params TypeReference[] parameters) {
      var module = customEditorAttributeType.Module;
      var constructor = customEditorAttributeType.Resolve().Methods.FirstOrDefault(m =>
          m.Name == ".ctor"
          && ParametersMatch(m, parameters));

      Assert.IsNotNull(constructor, $"Could not find a suitable constructor for attribute {customEditorAttributeType.Name} with parameters " +
        $"{string.Join(", ", parameters.Select(p => p.Name))}");

      var ctor = module.ImportReference(constructor);
      var attr = new CustomAttribute(ctor);
      foreach (var p in parameters) {
        attr.ConstructorArguments.Add(
          new CustomAttributeArgument(
            p.GetElementType(),
            p));
      }
      return attr;
    }

    public static string GetQualifiedName(TypeReference type) {
      if (type is GenericInstanceType g) {
        var args = g.GenericArguments.Select(GetType).ToArray();
        return $"{g.ElementType.FullName}[[{string.Join("],[", args.Select(t => t.AssemblyQualifiedName))}]]";
      }
      return $"{type.FullName}, {type.Module.Assembly.FullName}";
    }

    public static IEnumerable<TypeReference> GetBaseTypes(TypeDefinition typeDef) =>
      typeDef?.BaseType != null ? new[] { typeDef.BaseType }.Union(typeDef.BaseType.GetBaseTypes()) : Enumerable.Empty<TypeReference>();

    public static IEnumerable<TypeReference> GetBaseTypes(TypeReference typeRef) =>
      GetBaseTypes(typeRef.Resolve());

    public static TypeReference FindBaseTypeInHierarchy(TypeDefinition pivotType, TypeDefinition baseType, TypeReference @default = default) =>
      pivotType.GetBaseTypes().FirstOrDefault(t => t.Resolve()?.EqualsToType(baseType) ?? false) ?? @default;

    public static Type GetType(TypeReference type) =>
      Type.GetType(GetQualifiedName(type)) ?? GetType(type.Resolve());

    public static Type GetType(TypeDefinition type) =>
      Type.GetType(type.FullName + ", " + type.Module.Assembly.FullName);

    public static Type GetType(System.Reflection.Assembly assembly, TypeReference type) =>
      assembly.GetType(GetQualifiedName(type)) ?? assembly.GetType(GetQualifiedName(type.Resolve()));

    public static bool IsAssignableFrom(TypeReference sourceType, TypeReference targetType) {
      // Resolve the TypeReference to get a TypeDefinition
      var targetDefinition = targetType.Resolve();
      var sourceDefinition = sourceType.Resolve();

      if (sourceDefinition == targetDefinition) {
        return true;
      }

      var baseType = sourceDefinition.BaseType;
      if (baseType != null && IsAssignableFrom(baseType, targetType))
        return true;

      return sourceDefinition.Interfaces.Select(i => i.InterfaceType).Any(i => i.EqualsToType(targetType) ||
        i.Resolve() == targetDefinition &&
        (i is GenericInstanceType gi) && (targetType is GenericInstanceType gt) &&
        gi.HasGenericArguments && gt.HasGenericArguments &&
        gi.GenericArguments.Count() == gt.GenericArguments.Count() &&
        gi.GenericArguments.Select((t, idx) => IsAssignableFrom(t, gt.GenericArguments.ElementAt(idx))).All(v => v)
      );
    }
  }
}
