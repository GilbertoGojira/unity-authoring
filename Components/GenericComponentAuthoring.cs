using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Gil.Authoring.Components {

  public interface IGenericComponentAuthoring {
    void SetObjValue(string key, UnityEngine.Object value);
    bool TryGetObjValue(string key, out UnityEngine.Object value);
  } 

  public abstract class GenericComponentAuthoring<TCOMP> : MonoBehaviour, IGenericComponentAuthoring where TCOMP : struct {
    [Serializable]
    struct Tuple {
      public string Key;
      public UnityEngine.Object Value;
    }

    [SerializeField]
    [HideInInspector]
    List<Tuple> m_objectMap = new();

    public void SetObjValue(string key, UnityEngine.Object value) {
      m_objectMap.RemoveAll(t => t.Key == key);
      if (value != null)
        m_objectMap.Add(new Tuple { Key = key, Value = value });
    }

    public bool TryGetObjValue(string key, out UnityEngine.Object value) {
      var result = m_objectMap.FirstOrDefault(t => t.Key == key);
      value = result.Value;
      return !result.Equals(default);
    }
  }

  public abstract class GenericBaker<T> : Baker<T> where T : Component, IGenericComponentAuthoring {
    /// <summary>
    /// Type map with interfaces and its respective handling method
    /// </summary>
    static readonly (Type Type, string MethodName)[] m_possibleInterfaces = new[] {
        (typeof(IComponentData), nameof(AddGenericComponent)),
        (typeof(IBufferElementData), nameof(AddGenericBuffer))
    };

    void AddGenericComponent<TCOMP>(T authoring, TCOMP component, string name) where TCOMP : unmanaged, IComponentData {
      UpdateComponentValues(authoring, ref component, name);
      AddComponent(
        GetEntity(TransformUsageFlags.Dynamic),
        component);
    }

    void AddGenericBuffer<TBUFF>(T authoring, IEnumerable<TBUFF> elements, string name) where TBUFF : unmanaged, IBufferElementData {
      var buffer = elements.ToArray();
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
        var componentType = Utility.GetGenericMemberTypeArgument(member);
        var methodInfo = GetSuitableMethodForComponent(componentType);
        Assert.IsTrue(methodInfo != null, $"Member {name} does not implement any of the interfaces:\n" +
          $"{m_possibleInterfaces.Select(v => v.Type.ToString()).Aggregate((acc, v) => $"{acc}\n{v}")}");
        var component = Utility.GetMemberValue(member, authoring);
        // Get concrete generic method
        var genericMethod = methodInfo.MakeGenericMethod(componentType);
        // Invoke method with concrete values
        genericMethod.Invoke(this, new[] { authoring, component, name });
      }
    }

    static MethodInfo GetSuitableMethodForComponent(Type componentType) {
      var methodName = m_possibleInterfaces.FirstOrDefault(i => i.Type.IsAssignableFrom(componentType)).MethodName;
      return !string.IsNullOrEmpty(methodName) ? typeof(GenericBaker<T>)
          .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance) : default;
      
    }
  }
}