using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Gil.Authoring {

  public static class Utility {

    /// <summary>
    /// Formats property name
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static string FormatPropertyName(string propertyName) {
      // Handle variables staring with m_ and m[Caps]
      if (propertyName.StartsWith("m") && char.IsUpper(propertyName[1]))
        propertyName = char.ToUpper(propertyName[1]) + propertyName[2..];
      if (propertyName.StartsWith("m_"))
        propertyName = propertyName[2..];
      return Regex.Replace(propertyName, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }

    public static Type GetMemberType(MemberInfo m) {
      if (m is PropertyInfo p)
        return p.PropertyType;
      else if (m is FieldInfo f)
        return f.FieldType;
      return default;
    }

    public static object GetMemberValue(MemberInfo m, object instance) {
      var result = default(object);
      if (m is PropertyInfo p)
        result = p.GetValue(instance);
      else if (m is FieldInfo f)
        result = f.GetValue(instance);
      return Convert.ChangeType(result, GetMemberType(m));
    }

    public static object SetMemberValue(MemberInfo m, object instance, object value) {
      if (m is PropertyInfo p)
        p.SetValue(instance, value);
      else if (m is FieldInfo f)
        f.SetValue(instance, value);
      else
        throw new Exception($"Can't set member value of {m}");
      return instance;
    }

    public static IDictionary<string, MemberInfo> GetSerializableMembers<T>(bool declareOnly = false) =>
      GetSerializableMembers(typeof(T), declareOnly);

    public static IDictionary<string, MemberInfo> GetSerializableMembers(Type type, bool declareOnly = false) {
      var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
      return type.GetMembers(declareOnly ? flags | BindingFlags.DeclaredOnly : flags)
       .Where(m => m.MemberType == MemberTypes.Property || m.MemberType == MemberTypes.Field)
       .Where(m => Attribute.IsDefined(m, typeof(SerializeField)) || (m is FieldInfo f) && f.IsPublic || (m is PropertyInfo p) && p.GetMethod.IsPublic)
       .ToDictionary(m => m.Name, m => m);
    }
  }
}
