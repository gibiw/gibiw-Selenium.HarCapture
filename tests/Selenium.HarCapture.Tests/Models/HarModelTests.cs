using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Tests.Models;

/// <summary>
/// Tests that verify HAR model classes follow expected conventions.
/// </summary>
public sealed class HarModelTests
{
    private static readonly Type[] ModelTypes = typeof(Har).Assembly
        .GetTypes()
        .Where(t => t.Namespace == "Selenium.HarCapture.Models" && t.IsClass && !t.IsAbstract)
        .ToArray();

    [Fact]
    public void AllModelClasses_AreSealed()
    {
        foreach (var type in ModelTypes)
        {
            type.IsSealed.Should().BeTrue($"{type.Name} should be sealed for performance optimization");
        }
    }

    [Fact]
    public void AllProperties_HaveJsonPropertyNameAttribute()
    {
        foreach (var type in ModelTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var hasAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>() != null;
                hasAttribute.Should().BeTrue($"{type.Name}.{property.Name} should have [JsonPropertyName] attribute");
            }
        }
    }

    [Fact]
    public void NullableProperties_HaveJsonIgnoreWhenWritingNull()
    {
        var context = new NullabilityInfoContext();

        foreach (var type in ModelTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var nullabilityInfo = context.Create(property);
                var isNullable = nullabilityInfo.WriteState == NullabilityState.Nullable;

                if (isNullable)
                {
                    var jsonIgnoreAttr = property.GetCustomAttribute<JsonIgnoreAttribute>();
                    var hasCorrectCondition = jsonIgnoreAttr?.Condition == JsonIgnoreCondition.WhenWritingNull;

                    hasCorrectCondition.Should().BeTrue(
                        $"{type.Name}.{property.Name} is nullable and should have [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]");
                }
            }
        }
    }

    [Fact]
    public void NoModelClass_HasParameterizedConstructor()
    {
        foreach (var type in ModelTypes)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            // Either no explicit constructors (implicit parameterless), or only a parameterless one
            var hasParameterizedConstructor = constructors.Any(c => c.GetParameters().Length > 0);

            hasParameterizedConstructor.Should().BeFalse(
                $"{type.Name} should not have parameterized constructors (use init-only properties instead)");
        }
    }
}
