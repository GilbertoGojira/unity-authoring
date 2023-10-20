using Gil.Authoring.CodeGen;
using Gil.Authoring.Components;
using NUnit.Framework;
using Unity.Entities;

namespace Gil.Authoring.Tests {
  public struct MyComponent : IComponentData {
    public int IntValue;
    public float FloatValue;
  }

  public class MyAuthoringComponent : GenericComponentAuthoring<MyComponent> { }

  public struct MyBufferElement : IBufferElementData {
    public int IntValue;
    public float FloatValue;
  }

  public class MyAuthoringBufferElement : GenericComponentAuthoring<MyBufferElement> { }

  static class PostProcessTests
  {

    [Test]
    public static void PostProcessComponents() {
      ILComponentPostProcessor.PostProcessAssembly(Setup.AssemblyDefinition);
      Assert.True(true);
    }
  }
}
