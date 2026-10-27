#Requires -Version 7

# Fails when a class layout of the assembly (base type, interfaces, fields) uses a type from an assembly that may lack it at
# runtime, compiler-generated classes (lambdas, closures, async methods) included. See the rule in Plugin.cs
param(
    [Parameter(Mandatory)]
    [string]$AssemblyPath,

    [string[]]$Forbidden = @('DynamicMaps', 'Fika.Core', 'MapVariants.Client')
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

public static class LoadSafety
{
    public static List<string> FindViolations(string path, string[] forbidden)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var names = new TypeNames();
        var violations = new List<string>();

        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            var uses = new List<(string What, string Type)>();
            if (!type.BaseType.IsNil)
                uses.Add(("base type", names.Of(md, type.BaseType)));
            foreach (var implementation in type.GetInterfaceImplementations())
                uses.Add(("interface", names.Of(md, md.GetInterfaceImplementation(implementation).Interface)));
            foreach (var fieldHandle in type.GetFields())
            {
                var field = md.GetFieldDefinition(fieldHandle);
                uses.Add(($"field {md.GetString(field.Name)}", field.DecodeSignature(names, null)));
            }

            foreach (var (what, used) in uses)
            {
                if (forbidden.Any(assembly => used.Contains($"[{assembly}]")))
                    violations.Add($"{names.GetTypeFromDefinition(md, handle, 0)} {what} uses {used}");
            }
        }

        return violations;
    }
}

// Type names prefixed with their assembly when referenced from another one: [Assembly]Namespace.Name
public sealed class TypeNames : ISignatureTypeProvider<string, object>
{
    public string Of(MetadataReader md, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeFromDefinition(md, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => GetTypeFromReference(md, (TypeReferenceHandle)handle, 0),
        _ => GetTypeFromSpecification(md, null, (TypeSpecificationHandle)handle, 0),
    };

    public string GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var type = md.GetTypeDefinition(handle);
        var declaring = type.GetDeclaringType();
        return declaring.IsNil
            ? Qualify(md.GetString(type.Namespace), md.GetString(type.Name))
            : $"{GetTypeFromDefinition(md, declaring, rawTypeKind)}+{md.GetString(type.Name)}";
    }

    public string GetTypeFromReference(MetadataReader md, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var type = md.GetTypeReference(handle);
        var name = Qualify(md.GetString(type.Namespace), md.GetString(type.Name));
        return type.ResolutionScope.Kind switch
        {
            HandleKind.TypeReference => $"{GetTypeFromReference(md, (TypeReferenceHandle)type.ResolutionScope, rawTypeKind)}+{name}",
            HandleKind.AssemblyReference => $"[{md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name)}]{name}",
            _ => name,
        };
    }

    public string GetTypeFromSpecification(MetadataReader md, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        md.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        $"{genericType}<{string.Join(", ", typeArguments)}>";

    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetByReferenceType(string elementType) => $"{elementType}&";
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetPinnedType(string elementType) => elementType;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetFunctionPointerType(MethodSignature<string> signature) =>
        $"delegate*<{string.Join(", ", signature.ParameterTypes.Append(signature.ReturnType))}>";
    public string GetGenericMethodParameter(object genericContext, int index) => $"!!{index}";
    public string GetGenericTypeParameter(object genericContext, int index) => $"!{index}";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    private static string Qualify(string ns, string name) => ns.Length > 0 ? $"{ns}.{name}" : name;
}
'@

$violations = [LoadSafety]::FindViolations($AssemblyPath, $Forbidden)
foreach ($violation in $violations) {
    # MSBuild's canonical error format, so the build lists each one as an error
    Write-Host "$AssemblyPath : error LOADSAFETY: $violation"
}

if ($violations.Count -gt 0) {
    exit 1
}

Write-Host "Load safety: no class layout uses $($Forbidden -join ', ')"
