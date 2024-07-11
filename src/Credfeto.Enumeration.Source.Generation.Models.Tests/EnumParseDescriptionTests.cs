using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Xunit;

namespace Credfeto.Enumeration.Source.Generation.Models.Tests;

[SuppressMessage(category: "FunFair.CodeAnalysis", checkId: "FFS0013: Test classes should be derived from TestBase", Justification = "Not in this case")]
public sealed class EnumParseDescriptionTests
{
    [Fact]
    public void ParseDescriptionForAliased()
    {
        Assert.Equal(expected: ExampleEnumValues.ONE, ExampleEnumValuesGeneratedExtensions.ParseFromDescription("One \"1\""));
        Assert.Equal(expected: ExampleEnumValues.SAME_AS_ONE, ExampleEnumValuesGeneratedExtensions.ParseFromDescription("One \"1\""));
    }

    [Fact]
    public void ParseDescriptionForUnAliased()
    {
        Assert.Equal(expected: ExampleEnumValues.ZERO, ExampleEnumValuesGeneratedExtensions.ParseFromDescription("ZERO"));
        Assert.Equal(expected: ExampleEnumValues.THREE, ExampleEnumValuesGeneratedExtensions.ParseFromDescription("Two but one better!"));
    }

    [Fact]
    public void ParseDescriptionForUnknown()
    {
#if NET7_0_OR_GREATER
        Assert.Throws<UnreachableException>(() => ExampleEnumValuesGeneratedExtensions.ParseFromDescription("UNKNOWN"));
        #else
        Assert.Throws<ArgumentOutOfRangeException>(() => ExampleEnumValuesGeneratedExtensions.ParseFromDescription("UNKNOWN"));
#endif
    }

    [Fact]
    public void ParseDescriptionForExternalUnAliased()
    {
        Assert.Equal(expected: HttpStatusCode.OK, EnumExtensions.ParseFromDescription("OK"));
        Assert.Equal(expected: HttpStatusCode.Accepted, EnumExtensions.ParseFromDescription("Accepted"));
    }
}