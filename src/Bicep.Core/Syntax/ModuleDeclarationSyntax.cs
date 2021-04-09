// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using Bicep.Core.Extensions;
using Bicep.Core.Navigation;
using Bicep.Core.Parsing;
using Bicep.Core.Semantics;
using Bicep.Core.TypeSystem;

namespace Bicep.Core.Syntax
{
    public class ModuleDeclarationSyntax : StatementSyntax, ITopLevelNamedDeclarationSyntax
    {
        public ModuleDeclarationSyntax(IEnumerable<SyntaxBase> leadingNodes, Token keyword, IdentifierSyntax name, SyntaxBase path, SyntaxBase assignment, SyntaxBase value, Uri? templateUri = null)
            : base(leadingNodes)
        {
            AssertKeyword(keyword, nameof(keyword), LanguageConstants.ModuleKeyword);
            AssertSyntaxType(name, nameof(name), typeof(IdentifierSyntax));
            AssertSyntaxType(path, nameof(path), typeof(StringSyntax), typeof(SkippedTriviaSyntax));
            AssertTokenType(keyword, nameof(keyword), TokenType.Identifier);
            AssertSyntaxType(assignment, nameof(assignment), typeof(Token), typeof(SkippedTriviaSyntax));
            AssertTokenType(assignment as Token, nameof(assignment), TokenType.Assignment);
            AssertSyntaxType(value, nameof(value), typeof(SkippedTriviaSyntax), typeof(ObjectSyntax), typeof(IfConditionSyntax), typeof(ForSyntax));

            this.Keyword = keyword;
            this.Name = name;
            this.Path = path;
            this.Assignment = assignment;
            this.Value = value;
            this.TemplateUri = templateUri;
        }

        public Token Keyword { get; }

        public IdentifierSyntax Name { get; }

        public SyntaxBase Path { get; }

        public SyntaxBase Assignment { get; }
        
        public SyntaxBase Value { get; }

        /// <summary>
        /// The URI of the JSON template from which the module will be converted. This is only used in the decompiler.
        /// </summary>
        public Uri? TemplateUri { get; }

        public override void Accept(ISyntaxVisitor visitor) => visitor.VisitModuleDeclarationSyntax(this);

        public override TextSpan Span => TextSpan.Between(this.LeadingNodes.FirstOrDefault() ?? this.Keyword, this.Value);

        public StringSyntax? TryGetPath() => Path as StringSyntax;

        public TypeSymbol GetDeclaredType(ResourceScope containingScope, SemanticModel moduleSemanticModel)
        {
            var paramTypeProperties = new List<TypeProperty>();
            foreach (var param in moduleSemanticModel.Root.ParameterDeclarations.DistinctBy(p => p.Name))
            {
                var typePropertyFlags = TypePropertyFlags.WriteOnly;
                if (SyntaxHelper.TryGetDefaultValue(param.DeclaringParameter) == null)
                {
                    // if there's no default value, it must be specified
                    typePropertyFlags |= TypePropertyFlags.Required;
                }

                paramTypeProperties.Add(new TypeProperty(param.Name, param.Type, typePropertyFlags));
            }

            var outputTypeProperties = new List<TypeProperty>();
            foreach (var output in moduleSemanticModel.Root.OutputDeclarations.DistinctBy(o => o.Name))
            {
                outputTypeProperties.Add(new TypeProperty(output.Name, output.Type, TypePropertyFlags.ReadOnly));
            }

            return LanguageConstants.CreateModuleType(paramTypeProperties, outputTypeProperties, moduleSemanticModel.TargetScope, containingScope, "module");
        }

        public ObjectSyntax? TryGetBody() =>
            this.Value switch
            {
                ObjectSyntax @object => @object,
                IfConditionSyntax ifCondition => ifCondition.Body as ObjectSyntax,
                ForSyntax @for => @for.Body switch
                {
                    ObjectSyntax @object => @object,
                    IfConditionSyntax ifCondition => ifCondition.Body as ObjectSyntax,
                    SkippedTriviaSyntax => null,

                    _ => throw new NotImplementedException($"Unexpected type of for-expression value '{this.Value.GetType().Name}'.")
                },
                SkippedTriviaSyntax => null,

                // blocked by assert in the constructor
                _ => throw new NotImplementedException($"Unexpected type of module value '{this.Value.GetType().Name}'.")
            };

        public ObjectSyntax GetBody() =>
            this.TryGetBody() ?? throw new InvalidOperationException($"A valid module body is not available on this module due to errors. Use {nameof(TryGetBody)}() instead.");
    }
}
