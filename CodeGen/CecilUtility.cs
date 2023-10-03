using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Assertions;

namespace Gil.Authoring.CodeGen {

  public static class CecilUtility {

    public static TypeDefinition CreateTypeWithDefaultConstructor(string @namespace, string name, TypeReference baseType) {
      var newType = new TypeDefinition(
        @namespace,
        name,
        TypeAttributes.Class | TypeAttributes.Public,
        baseType);

      var baseCtor = baseType.Module.ImportReference(baseType.Resolve().GetConstructors().First());
      baseCtor.DeclaringType = baseType;

      // Define the default constructor method (ctor) within the class
      var ctor = new MethodDefinition(
          ".ctor",                                // Name of the constructor
          MethodAttributes.Public |               // Specify the method attributes (e.g., Public)
          MethodAttributes.HideBySig |            // HideBySig is required for constructors
          MethodAttributes.SpecialName |          // SpecialName is required for constructors
          MethodAttributes.RTSpecialName,         // RTSpecialName is required for constructors
          baseType.Module.ImportReference(typeof(void))     // Return type (void for constructors)
      );

      // Add the constructor's body (e.g., to call base constructor)
      // Here, we call the constructor of the base class (Object) using IL code
      ctor.Body = new MethodBody(ctor);
      ctor.Body.GetILProcessor().Emit(OpCodes.Ldarg_0);
      ctor.Body.GetILProcessor().Emit(OpCodes.Call, baseCtor);
      ctor.Body.GetILProcessor().Emit(OpCodes.Ret);

      // Add the constructor to the class
      newType.Methods.Add(ctor);

      return newType;
    }

    /// <summary>
    /// Check if a method parameters match with the supplied ones
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static bool ParametersMatch(MethodDefinition method, TypeReference[] parameters) =>
      method.Parameters.Count == parameters.Count()
      && method.Parameters.Zip(parameters, (e1, e2) => e1.ParameterType.EqualsToType(typeof(Type)) || e2.IsAssignableFrom(e1.ParameterType))
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
      pivotType.GetBaseTypes().FirstOrDefault(t => t.Resolve().EqualsToType(baseType)) ?? @default;

    public static Type GetType(TypeReference type) =>
      Type.GetType(GetQualifiedName(type)) ?? GetType(type.Resolve());

    public static Type GetType(TypeDefinition type) =>
      Type.GetType(type.FullName + ", " + type.Module.Assembly.FullName);
  }
}
