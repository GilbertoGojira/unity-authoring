using Gil.Authoring.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Gil.Authoring.Components {

  public interface IGenericComponentAuthoring {
    void SetObjValue(string key, UnityEngine.Object value);
    bool TryGetObjValue(string key, out UnityEngine.Object value);
  } 

  public abstract class GenericComponentAuthoring : MonoBehaviour, IGenericComponentAuthoring {
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

    [CustomEditor(typeof(SampleComponent))]
    class GenericComponentAuthoringEditor : GenericInspectorEditor<SampleComponent> { }
  }

  public abstract class GenericBaker<T> : Baker<T> where T : GenericComponentAuthoring {

    void AddGenericComponent<TCOMP>(TCOMP component) where TCOMP : unmanaged, IComponentData {
      AddComponent(
        GetEntity(TransformUsageFlags.Dynamic),
        component);
    }

    void UpdateComponentValues(T authoring, object component, string sourceName) {
      // Add Entities related to gameobject or component references
      foreach (var (name, member) in Utility.GetSerializableMembers(component.GetType(), true)) {
        var fullName = $"{sourceName}/{name}";
        var type = Utility.GetMemberType(member);
        if ((type == typeof(Entity) || type.IsSubclassOf(typeof(Entity))) && authoring.TryGetObjValue(fullName, out var go))
          Utility.SetMemberValue(member, component, GetEntity(go as GameObject, TransformUsageFlags.Dynamic));
      }
    }

    public override void Bake(T authoring) {
      var methodInfo = typeof(GenericBaker<T>)
        .GetMethod(nameof(AddGenericComponent), BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (var (name, member) in Utility.GetSerializableMembers<T>(true)) {
        var component = Utility.GetMemberValue(member, authoring);
        UpdateComponentValues(authoring, component, name);
        // Get concrete generic method
        var genericMethod = methodInfo.MakeGenericMethod(Utility.GetMemberType(member));
        // Invoke method with concrete values
        genericMethod.Invoke(this, new[] { component });
      }
    }
  }
}