using System;

namespace Gil.Authoring.Components {

  [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
  sealed public class GenerateAuthoringAttribute : Attribute {
    public GenerateAuthoringAttribute() { }
  }
}
