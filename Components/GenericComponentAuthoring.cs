using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gil.Authoring.Components {

  public interface IGenericComponentAuthoring {
    void SetObjValue(string key, UnityEngine.Object value);
    bool TryGetObjValue(string key, out UnityEngine.Object value);
  }

  public abstract class GenericComponentAuthoring<TCOMP> : MonoBehaviour, IGenericComponentAuthoring {
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
}