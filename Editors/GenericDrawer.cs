#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Gil.Authoring.Components;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gil.Authoring.Editor {

  /// <summary>
  /// A generic property drawer for any structure
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class GenericDrawer<T> : PropertyDrawer {

    /// <summary>
    /// The label for drawer
    /// </summary>
    protected string Label;

    /// <summary>
    /// A map of member names to be overridden by new elements
    /// NOTE: Elements overridden here take precedence from type overrides
    /// </summary>
    protected Dictionary<string, VisualElement> OverrideElements = new();

    /// <summary>
    /// A map of member names to be overridden by new elements created on a lambda function
    /// NOTE: Elements overridden take precedence from type overrides
    /// </summary>
    protected Dictionary<Type, Func<SerializedProperty, string, VisualElement>> OverrideTypes = new();

    public GenericDrawer() {
      OverrideTypes.Add(typeof(Entity), (property, childPropertyName) => {
        var path = $"{property.propertyPath}.{childPropertyName}";
        var target = property.serializedObject.targetObject as IGenericComponentAuthoring;
        var Field = new ObjectField(childPropertyName.ToPrettyString()) {
          objectType = typeof(GameObject),
          value = target.TryGetObjValue(path, out var @object) ? @object : null
        };
        Field.RegisterValueChangedCallback(evt => {
          target.SetObjValue(path, evt.newValue);
          property.serializedObject.ApplyModifiedProperties();
          EditorUtility.SetDirty(property.serializedObject.targetObject);
        });
        return Field;
      });
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
      // Create a new VisualElement to be the root the property UI
      var container = new VisualElement();

      // Create drawer UI using C#
      var popup = new UnityEngine.UIElements.PopupWindow {
        text = string.IsNullOrEmpty(Label) ? property.displayName : Label
      };
      
      foreach (var (childPropertyName, m) in Utility.GetSerializableMembers<T>()) {
        if (!OverrideElements.TryGetValue(childPropertyName, out VisualElement newElement))
          newElement = (OverrideTypes.TryGetValue(Utility.GetMemberType(m), out var func) ? func : null)?.Invoke(property, $"{childPropertyName}") ??
            new PropertyField(property.FindPropertyRelative(childPropertyName));
        popup.Add(newElement);
      }
      container.Add(popup);

      // Return the finished UI
      return container;
    }
  }

  /*
  public abstract class GenericInspectorEditor<T> : UnityEditor.Editor {

    public override VisualElement CreateInspectorGUI() {
      // Create a new VisualElement to be the root the property UI
      var container = new VisualElement();

      // Create drawer UI using C#
      var popup = new UnityEngine.UIElements.PopupWindow { };
      foreach (var (propertyName, m) in Utility.GetSerializableMembers<T>()) {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null) {
          if (Utility.IsAssignableFrom(Utility.GetMemberType(m), typeof(IEnumerable<>)))
            popup.Add(new PropertyField(property));
          else {
            // Create an instance of a generic component drawer for the given type
            var genericType = typeof(GenericDrawer<>).MakeGenericType(Utility.GetMemberType(m));
            var instance = Activator.CreateInstance(genericType) as PropertyDrawer;
            // Creates the elements for the type
            var newElement = instance.CreatePropertyGUI(property);
            popup.Add(newElement);
          }
        }
      }
      container.Add(popup);

      serializedObject.ApplyModifiedProperties();

      return container;
    }
  }*/
}
#endif