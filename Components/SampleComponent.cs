using System;
using Unity.Entities;

namespace Gil.Authoring.Components {

  [Serializable]
  public struct Sample : IComponentData {
    public int IntValue;
    public float FloatValue;
    public Entity Entity;
  }

  public class SampleComponent : GenericComponentAuthoring {
    public Sample Value;

    public class Baker : GenericBaker<SampleComponent> { }
  }
}