using System;
using System.Collections.Generic;
using System.Reflection;

namespace Gil.Authoring {

  public static class Extensions {

    /// <summary>
    /// Return a formatted string 
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string ToPrettyString(this string str) =>
      Utility.FormatPropertyName(str);

    public static Type GetMemberType(this MemberInfo memberInfo) =>
      Utility.GetMemberType(memberInfo);

    public static object GetMemberValue(this MemberInfo memberInfo, object instance) =>
      Utility.GetMemberValue(memberInfo, instance);

    public static object SetMemberValue(this MemberInfo memberInfo, object instance, object value) =>
      Utility.SetMemberValue(memberInfo, instance, value);

    public static IDictionary<string, MemberInfo> GetSerializableMembers(this Type type, bool declareOnly = false) =>
      Utility.GetSerializableMembers(type, declareOnly);

    public static IDictionary<string, MemberInfo> GetSerializableMembers<T>(this T _, bool declareOnly = false) =>
      Utility.GetSerializableMembers<T>(declareOnly);


  }
}
