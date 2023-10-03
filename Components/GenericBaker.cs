using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Gil.Authoring.Components {

  internal static class BakerConstraints {
    /// <summary>
    /// Type map with interfaces and its respective handling method
    /// </summary>
    internal static readonly (Type Type, string MethodName)[] PossibleInterfaces = new[] {
        (typeof(IComponentData), "AddGenericComponent"),
        (typeof(IEnumerable<IBufferElementData>), "AddGenericBuffer")
    };
  }

  public abstract class GenericBaker<T> : Baker<T> where T : Component, IGenericComponentAuthoring {

    void AddGenericComponent<TCOMP>(T authoring, TCOMP component, string name) where TCOMP : unmanaged, IComponentData {
      UpdateComponentValues(authoring, ref component, name);
      AddComponent(
        GetEntity(TransformUsageFlags.Dynamic),
        component);
    }

    void AddGenericBuffer<TBUFF>(T authoring, IEnumerable<TBUFF> elements, string name) where TBUFF : unmanaged, IBufferElementData {
      var buffer = elements?.ToArray() ?? new TBUFF[0];
      UpdateBufferValues(authoring, buffer, name);
      AddBuffer<TBUFF>(GetEntity(TransformUsageFlags.Dynamic))
        .AddRange(new NativeArray<TBUFF>(buffer, Allocator.Temp));
    }

    void UpdateBufferValues<TBUFF>(T authoring, TBUFF[] component, string sourceName) {
      for (var i = 0; i < component.Count(); ++i) {
        var current = component.ElementAt(i);
        UpdateComponentValues(authoring, ref current, $"{sourceName}.Array.data[{i}]");
        component[i] = current;
      }
    }

    void UpdateComponentValues<TCOMP>(T authoring, ref TCOMP component, string sourceName) {
      // Add Entities related to gameobject or component references
      foreach (var (name, member) in Utility.GetSerializableMembers(component.GetType(), true)) {
        var fullName = $"{sourceName}.{name}";
        var type = Utility.GetMemberType(member);
        if ((type == typeof(Entity) || type.IsSubclassOf(typeof(Entity))) && authoring.TryGetObjValue(fullName, out var go))
          component = (TCOMP)Utility.SetMemberValue(member, component, GetEntity(go as GameObject, TransformUsageFlags.Dynamic));
      }
    }

    public override void Bake(T authoring) {
      foreach (var (name, member) in Utility.GetSerializableMembers<T>(true)) {
        var memberType = Utility.GetMemberType(member);
        var methodInfo = GetSuitableMethodForComponent(memberType);
        Assert.IsTrue(methodInfo != null, $"Member {name} does not implement any of the interfaces:\n" +
          $"{BakerConstraints.PossibleInterfaces.Select(v => v.Type.ToString()).Aggregate((acc, v) => $"{acc}\n{v}")}");
        var component = Utility.GetMemberValue(member, authoring);
        // Get concrete generic method
        var componentType = Utility.GetGenericMemberTypeArgument(member);
        var genericMethod = methodInfo.MakeGenericMethod(componentType);
        // Invoke method with concrete values
        genericMethod.Invoke(this, new[] { authoring, component, name });
      }
    }

    static MethodInfo GetSuitableMethodForComponent(Type componentType) {
      var methodName = BakerConstraints.PossibleInterfaces.FirstOrDefault(i => Utility.IsAssignableFrom(componentType, i.Type)).MethodName;
      return !string.IsNullOrEmpty(methodName) ? typeof(GenericBaker<T>)
          .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance) : default;

    }
  }
}
