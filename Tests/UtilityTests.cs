using Gil.Authoring.CodeGen;
using Gil.Authoring.Components;
using NUnit.Framework;
using UnityEngine;

namespace Gil.Authoring.Tests {

  static class UtilityTests {

    interface IGeneric<T> { }

    interface IAnotherGeneric<T> { }

    interface IGeneric<T1, T2> { }

    interface ISample01 { }

    interface ISample02 { }

    struct Sample01 : ISample01 { }

    struct Sample02 : ISample02 { }

    struct AnotherSample : IGeneric<Sample01> { }

    struct YetAnotherSample : IGeneric<Sample01, Sample02> { }

    class MyClass01 : ISample01 { }

    class MyClass02 : MyClass01 { }

    class Authoring : GenericComponentAuthoring<Sample01> { }

    [Test]
    public static void IsAssignableFromInterface() {

      // Simple interfaces
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(Sample01));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(ISample01));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(Sample01));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(ISample02));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.False(test);
    }

    [Test]
    public static void IsAssignableFromInterfaceWithGeneric() {
      // Interface with generics
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(AnotherSample));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(IGeneric<ISample01>));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(AnotherSample));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(IGeneric<Sample01>));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(AnotherSample));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(IGeneric<ISample02>));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.False(test);
    }

    [Test]
    public static void IsAssignableFromInterfaceWithMultipleGeneric() {
      // Interface with generics
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(YetAnotherSample));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(IGeneric<ISample01, ISample02>));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(YetAnotherSample));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(IGeneric<ISample02, ISample01>));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.False(test);
    }

    [Test]
    public static void IsAssignableFromDeepInterface() {
      // Interface with generics
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(MyClass02));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(ISample01));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(MyClass02));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(ISample02));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.False(test);
    }

    [Test]
    public static void IsAssignableFromAuthoring() {
      // Interface with generics
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(Authoring));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(IGenericComponentAuthoring));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(Authoring));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(ISample02));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.False(test);
    }

    [Test]
    public static void IsAssignableFromExternal() {
      // Interface with generics
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(Authoring));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(MonoBehaviour));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);
    }

    [Test]
    public static void IsAssignableFromSimilarInterfaces() {
      // Interface with generics
      var rootType = Setup.ModuleDefinition.ImportReference(typeof(AnotherSample));
      var @interface = Setup.ModuleDefinition.ImportReference(typeof(IGeneric<Sample01>));
      var test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.True(test);

      rootType = Setup.ModuleDefinition.ImportReference(typeof(AnotherSample));
      @interface = Setup.ModuleDefinition.ImportReference(typeof(IAnotherGeneric<Sample01>));
      test = CecilUtility.IsAssignableFrom(rootType, @interface);
      Assert.False(test);
    }
  }
}
