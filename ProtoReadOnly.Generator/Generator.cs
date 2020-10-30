using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace ProtobufExtensionGenerator
{
    internal record Property(TypeData Type, string Name);
    internal record TypeData(string Name, MapTypeData? MapTypeData);
    internal record MapTypeData(string MapKeyType, string MapValueOriginalType, string MapValueReadOnlyType);

    [Generator]
    public class ProtobufGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var messageNodes = LocateMessages(context.Compilation);
            var readonlyMap = GenerateReadOnlyMap(messageNodes);
            var generated = Generate(messageNodes, readonlyMap);

            foreach (var (hintName, source) in generated)
            {
                context.AddSource(hintName, source);
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Nothing to initialize
        }

        private IEnumerable<ClassDeclarationSyntax> LocateMessages(Compilation compilation)
        {
            IEnumerable<SyntaxNode>? allNodes = compilation.SyntaxTrees.SelectMany(s => s.GetRoot().DescendantNodes());
            var rawNodes = allNodes.Where(d => d.IsKind(SyntaxKind.ClassDeclaration)).OfType<ClassDeclarationSyntax>();

            return rawNodes
                .Where(node => node.BaseList?.Types != null)
                .Where(node => node.BaseList?.Types.FirstOrDefault(baseType =>
                {
                    var type = baseType.Type as AliasQualifiedNameSyntax;
                    var alias = type?.Alias.ToString();
                    var name = type?.Name as GenericNameSyntax;
                    return alias == "pb" && (name?.Identifier.ToString() == "IMessage");
                }) != null);
        }

        private IReadOnlyDictionary<string, string> GenerateReadOnlyMap(IEnumerable<ClassDeclarationSyntax> messageNodes)
        {
            return messageNodes.ToDictionary(node =>
            {
                var name = node.Identifier.ToString();
                var parentNs = node.Parent as NamespaceDeclarationSyntax;
                return $"global::{parentNs?.Name}.{name}";
            }, node =>
            {
                var name = node.Identifier.ToString();
                var parentNs = node.Parent as NamespaceDeclarationSyntax;
                return $"global::{parentNs?.Name}.IReadOnly{name}";
            });
        }

        private IEnumerable<(string, string)> Generate(IEnumerable<ClassDeclarationSyntax> messageNodes, IReadOnlyDictionary<string, string> messageMap)
        {
            return messageNodes
                .Where(node => node.Parent as NamespaceDeclarationSyntax != null)
                .Select(node =>
            {
                //Only works for namespace parents for the time being
                var name = node.Identifier.ToString();
                var parentNs = node.Parent as NamespaceDeclarationSyntax;
                var nsName = parentNs!.Name.ToString();

                var properties = node.Members
                    .Where(s => s.IsKind(SyntaxKind.PropertyDeclaration)).OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Modifiers.Where(t => t.ToString() == "static").Count() == 0)
                    .Where(p => p.Type.ToString() != "pbr::MessageDescriptor")
                    .Select(p =>
                    {
                        return new Property(MapPropertyType(p.Type, messageMap, $"{nsName}.{name}"), p.Identifier.ToString());
                    });

                var content = @$"
using System;
namespace {nsName} {{
    public interface IReadOnly{name} {{
        {properties.Select(p => $"{p.Type.Name} {p.Name} {{ get; }}").Aggregate("", (prod, next) => $"{prod}\n{next}")}
    }}

    public partial class {name} : IReadOnly{name} {{
        {properties.Select(p => $"{p.Type.Name} IReadOnly{name}.{p.Name} => {(p.Type.MapTypeData != null ? $"new ProtoReadOnly.ReadOnlyWrapper<{p.Type.MapTypeData.MapKeyType},{p.Type.MapTypeData.MapValueOriginalType},{p.Type.MapTypeData.MapValueReadOnlyType}>({p.Name})" : p.Name)};").Aggregate("", (prod, next) => $"{prod}\n{next}")}
    }}
}}
";
                var hintName = $"{nsName}.{name}";
                return (hintName, content);
            });
        }

        private TypeData MapPropertyType(TypeSyntax type, IReadOnlyDictionary<string, string> messageMap, string? parentName)
        {
            return type switch
            {
                AliasQualifiedNameSyntax aq => aq.Alias.ToString() == "pbc" ? MapProtoType(aq.Name, messageMap) : new TypeData(type.ToString(), null),
                QualifiedNameSyntax qn => new TypeData(messageMap.ContainsKey(qn.ToString()) ? messageMap[qn.ToString()] : qn.ToString(), null),
                _ => new TypeData(type.ToString().EndsWith("Case") && parentName != null ? $"{parentName}.{type}" : type.ToString(), null)
            };
        }

        private TypeData MapProtoType(SimpleNameSyntax type, IReadOnlyDictionary<string, string> messageMap)
        {
            return type switch
            {
                GenericNameSyntax gn => gn.Identifier.ToString() switch
                {
                    "MapField" => CalculateMapTypeData(gn, messageMap),
                    "RepeatedField" => new TypeData($"System.Collections.Generic.IReadOnlyCollection<{MapPropertyType(gn.TypeArgumentList.Arguments[0], messageMap, null).Name}>", null),
                    _ => new TypeData(type.ToString(), null)
                },
                _ => new TypeData(type.ToString(), null)
            };
        }

        private TypeData CalculateMapTypeData(GenericNameSyntax gn, IReadOnlyDictionary<string, string> messageMap)
        {
            var keyType = gn.TypeArgumentList.Arguments[0];
            var originalValueType = gn.TypeArgumentList.Arguments[1];
            var mappedType = MapPropertyType(originalValueType, messageMap, null);

            var result = $"System.Collections.Generic.IReadOnlyDictionary<{keyType},{mappedType.Name}>";

            return new TypeData(result, originalValueType.ToString() != mappedType.Name ? new MapTypeData(keyType.ToString(), originalValueType.ToString(), mappedType.Name) : null);
        }

    }
}
