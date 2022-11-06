# credfeto-enum-source-generation

C# Source generator for enums

## Using

Add a reference to the Credfeto.Enumeration.Source.Generation package in each project.

```xml
<ItemGroup>
  <PackageReference 
            Include="Credfeto.Enumeration.Source.Generation" 
            Version="0.0.2.3" 
            PrivateAssets="All" />
</ItemGroup>
```

For each enum in the project, generates a class with the following extension methods:

* public static string GetName(this MyEnum value)
* public static string GetDescription(this MyEnum value)

Given an the example enum defined below:

```csharp
public enum ExampleEnumValues
{
    ZERO = 0,

    [Description("One \"1\"")]
    ONE = 1,

    SAME_AS_ONE = ONE,
}
```

To get the name and value of the enum values. In release mode this can be practically instant.

```csharp
 string name = ExampleEnumValues.ONE.GetName(); // ONE
 string description = ExampleEnumValues.ONE.GetDescription(); // One "1"
```

## Benchmarks

Benchmarks are in the Benchmark.net project ``Credfeto.Enumeration.Source.Generation.Benchmarks``, with a summary of a
run below.

|                         Method |          Mean |      Error |     StdDev | Allocated |
|------------------------------- |--------------:|-----------:|-----------:|----------:|
|                GetNameToString |    24.4628 ns |  0.3071 ns |  0.2723 ns |      24 B |
|              GetNameReflection |    42.8955 ns |  0.2418 ns |  0.2144 ns |      24 B |
|        GetNameCachedReflection |    32.1275 ns |  0.2462 ns |  0.2056 ns |      24 B |
|           GetNameCodeGenerated |     0.0000 ns |  0.0000 ns |  0.0000 ns |         - |
|       GetDescriptionReflection | 1,439.7767 ns | 28.2353 ns | 27.7308 ns |     264 B |
| GetDescriptionCachedReflection |    31.9541 ns |  0.3890 ns |  0.3249 ns |      24 B |
|    GetDescriptionCodeGenerated |     0.0000 ns |  0.0000 ns |  0.0000 ns |         - |

```
// * Warnings *
ZeroMeasurement
EnumBench.GetNameCodeGenerated: Default -> The method duration is indistinguishable from the empty method duration
EnumBench.GetDescriptionCodeGenerated: Default -> The method duration is indistinguishable from the empty method
duration

```

### Viewing Compiler Generated files

Add the following to the csproj file:

```xml
  <PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <!-- Don't include the output from a previous source generator execution into future runs; the */** trick here ensures that there's
    at least one subdirectory, which is our key that it's coming from a source generator as opposed to something that is coming from
    some other tool. -->
    <Compile Remove="$(CompilerGeneratedFilesOutputPath)/*/**/*.cs" />
  </ItemGroup>
```

## Changelog

View [changelog](CHANGELOG.md)

