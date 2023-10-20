using System.Linq;
using Gil.Authoring.CodeGen;
using Mono.Cecil;
using NUnit.Framework;

namespace Gil.Authoring.Tests {

  [SetUpFixture]
  internal class Setup {
    public static AssemblyResolver AssemblyResolver;
    public static AssemblyDefinition AssemblyDefinition;
    public static ModuleDefinition ModuleDefinition;

    [OneTimeSetUp]
    public static void CreateTestMainModule() {
      if (ModuleDefinition != null)
        return;
      var paths = CecilUtility.GetAssemblyPaths(new[] { $"{nameof(Gil)}.{nameof(Authoring)}.{nameof(Tests)}" });
      AssemblyResolver = new AssemblyResolver(true);
      AssemblyDefinition = AssemblyResolver.AddAssembly(paths.First());
      ModuleDefinition = AssemblyDefinition.MainModule;
    }
  }
}
