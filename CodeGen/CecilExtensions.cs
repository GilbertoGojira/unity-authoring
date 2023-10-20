using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Gil.Authoring.CodeGen {

  public static class CecilExtensions {

    public static string GetUniqueName(this TypeDefinition typeDef, ModuleDefinition futureModule = null) =>
      $"{typeDef.FullName}/{(typeDef.Module ?? futureModule).Assembly.Name}";

    public static string GetUniqueName(this TypeReference typeRef) {
      var typeDef = (typeRef as TypeDefinition) ?? typeRef.Resolve();
      return $"{typeRef.FullName}/{typeDef.Module.Assembly.Name}";
    }
    public static bool EqualsToType(this TypeReference typeRef, Type type) =>
      typeRef.EqualsToType(typeRef.Module.ImportReference(type));

    public static bool EqualsToType(this TypeReference typeRef, TypeReference type) =>
      typeRef.GetUniqueName() == type.GetUniqueName();

    public static IEnumerable<TypeReference> GetBaseTypes(this TypeDefinition typeDef) =>
      CecilUtility.GetBaseTypes(typeDef);

    public static IEnumerable<TypeReference> GetBaseTypes(this TypeReference typeRef) =>
      CecilUtility.GetBaseTypes(typeRef);

    public static bool IsDerivedFrom(this TypeDefinition typeDef, Type type) =>
       typeDef?.GetBaseTypes().Any(t => t.EqualsToType(type)) ?? false;

    public static bool IsDerivedFrom(this TypeReference typeRef, TypeReference type) =>
       typeRef?.GetBaseTypes().Any(t => t.EqualsToType(type)) ?? false;

    public static bool IsAssignableFrom(this TypeReference targetType, Type sourceType) =>
      CecilUtility.IsAssignableFrom(targetType.Module.ImportReference(sourceType), targetType);

    public static bool IsAssignableFrom(this Type targetType, TypeReference sourceType) =>
      CecilUtility.IsAssignableFrom(sourceType, sourceType.Module.ImportReference(targetType));

    public static bool IsAssignableFrom(this TypeReference targetType, TypeReference sourceType) =>
      CecilUtility.IsAssignableFrom(sourceType, targetType);

    public static TypeDefinition Duplicate(this TypeDefinition typeDef) {
      // Create a new TypeDefinition with the same name, attributes, and base type
      TypeDefinition duplicatedTypeDefinition = new(
          typeDef.Namespace,
          typeDef.Name,
          typeDef.Attributes,
          typeDef.BaseType);

      // Copy other properties from the original TypeDefinition
      foreach (var p in typeDef.GenericParameters)
        duplicatedTypeDefinition.GenericParameters.Add(p);

      //foreach (var f in typeDef.Fields)
      //  duplicatedTypeDefinition.Fields.Add(f);

      foreach (var p in typeDef.Properties)
        duplicatedTypeDefinition.Properties.Add(p);

      foreach (var m in typeDef.Methods)
        duplicatedTypeDefinition.Methods.Add(m);

      foreach (var ca in typeDef.CustomAttributes)
        duplicatedTypeDefinition.CustomAttributes.Add(ca);

      // TODO: Check if there is more to copy
      return duplicatedTypeDefinition;
    }

  }
}