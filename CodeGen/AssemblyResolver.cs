using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

[assembly: InternalsVisibleTo("Gil.Authoring.Tests")]
namespace Gil.Authoring.CodeGen {

  internal class AssemblyResolver : BaseAssemblyResolver {
    Dictionary<string, AssemblyDefinition> m_usedAssemblyDefinitions;
    bool m_resolveAdditionalAssemblies;

    public IEnumerable<AssemblyDefinition> AssemblyDefinitions {
      get;
      private set;
    }

    public AssemblyResolver(bool resolveAdditionalAssemblies = false) =>
      Initialize(Enumerable.Empty<string>(), resolveAdditionalAssemblies);

    public AssemblyResolver(IEnumerable<string> assemblyPaths, bool resolveAdditionalAssemblies = false) =>
      Initialize(assemblyPaths, resolveAdditionalAssemblies);

    void Initialize(IEnumerable<string> assemblyPaths, bool resolveAdditionalAssemblies) {
      m_resolveAdditionalAssemblies = resolveAdditionalAssemblies;
      foreach (var path in assemblyPaths)
        AddSearchDirectory(path);
      m_usedAssemblyDefinitions = assemblyPaths.Select(path =>
        AssemblyDefinition.ReadAssembly(
          path,
          new ReaderParameters {
            AssemblyResolver = this
          }))
          .ToDictionary(a => a.FullName, a => a);
      AssemblyDefinitions = m_usedAssemblyDefinitions.Values.ToArray();
    }

    public AssemblyDefinition AddAssembly(string assemblyName, byte[] assemblyData, byte[] symbolData = null, bool readWrite = false) {
      if (m_usedAssemblyDefinitions.TryGetValue(assemblyName, out var assemblyDef))
        return assemblyDef;

      var peStream = new MemoryStream(assemblyData);

      var assembly = AssemblyDefinition.ReadAssembly(peStream, new ReaderParameters {
        ReadWrite = readWrite,
        AssemblyResolver = this,
        ReadingMode = ReadingMode.Immediate,
        ReadSymbols = symbolData != null,
        SymbolStream = symbolData != null ? new MemoryStream(symbolData) : default,
        SymbolReaderProvider = symbolData != null ? new PortablePdbReaderProvider() : default,
        ReflectionImporterProvider = new ReflectionImporterProvider()
      });
      AddSearchDirectory(Path.GetDirectoryName(assembly.MainModule.FileName));
      return AddAssembly(assembly);
    }

    public AssemblyDefinition AddAssembly(Assembly assembly, bool readWrite = false) {
      if (m_usedAssemblyDefinitions.TryGetValue(assembly.FullName, out var assemblyDef))
        return assemblyDef;
      return AddAssembly(assembly.Location, readWrite);
    }

    public AssemblyDefinition AddAssembly(string path, bool readWrite = false) {
      var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
        ReadWrite = readWrite,
        AssemblyResolver = this
      });
      AddSearchDirectory(path);
      return AddAssembly(assembly);
    }

    public AssemblyDefinition AddAssembly(AssemblyDefinition assembly) {
      if (m_usedAssemblyDefinitions.TryGetValue(assembly.FullName, out var usedAssembly)) {
        usedAssembly.Dispose();
        m_usedAssemblyDefinitions.Remove(usedAssembly.FullName);
      }

      m_usedAssemblyDefinitions.Add(
          assembly.FullName, assembly);
      return assembly;
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name) {
      if (m_usedAssemblyDefinitions.TryGetValue(name.FullName, out var assembly))
        return assembly;
      if (m_resolveAdditionalAssemblies) {
        var resolvedAssembly = AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(a => a.FullName == name.FullName);
        if (resolvedAssembly != null && !string.IsNullOrEmpty(resolvedAssembly.Location))
          return AddAssembly(resolvedAssembly.Location);
      }
      return null;
    }

    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      foreach (var assembly in m_usedAssemblyDefinitions.Values)
        assembly.Dispose();
      m_usedAssemblyDefinitions.Clear();
    }
  }
}
