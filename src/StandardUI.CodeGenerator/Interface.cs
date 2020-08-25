﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StandardUI.CodeGenerator
{
    public class Interface
    {
        public static int IndentSize = 4;
        public const string RootNamespace = "Microsoft.StandardUI";

        private readonly NameSyntax _sourceNamespaceName;
        private readonly CompilationUnitSyntax _sourceCompilationUnit;
        private readonly QualifiedNameSyntax _destinationNamespaceName;

        public Context Context { get; }
        public string DestinationClassName { get; }
        public InterfaceDeclarationSyntax Declaration { get; }
        public InterfaceDeclarationSyntax? AttachedInterfaceDeclaration { get; }
        public string Name { get; }
        public string VariableName { get; }

        public Interface(Context context, InterfaceDeclarationSyntax sourceInterfaceDeclaration, InterfaceDeclarationSyntax? sourceAttachedInterfaceDeclaration)
        {
            Context = context;
            Declaration = sourceInterfaceDeclaration;
            AttachedInterfaceDeclaration = sourceAttachedInterfaceDeclaration;

            Name = sourceInterfaceDeclaration.Identifier.Text;
            if (!Name.StartsWith("I"))
                throw new UserViewableException($"Data model interface {Name} must start with 'I'");

            DestinationClassName = Name.Substring(1);

            // Form the default variable name for the interface by dropping the "I" and lower casing the first letter after (ICanvas => canvas)
            VariableName = Name.Substring(1, 1).ToLower() + Name.Substring(2);

            if (!(Declaration.Parent is NamespaceDeclarationSyntax interfaceNamespaceDeclaration))
                throw new UserViewableException(
                    $"Parent of ${Name} interface should be namespace declaration, but it's a {Declaration.Parent.GetType()} node instead");
            _sourceNamespaceName = interfaceNamespaceDeclaration.Name;

            if (!(interfaceNamespaceDeclaration.Parent is CompilationUnitSyntax compilationUnit))
                throw new UserViewableException(
                    $"Parent of ${interfaceNamespaceDeclaration} namespace should be compilation unit, but it's a {interfaceNamespaceDeclaration.Parent.GetType()} node instead");
            _sourceCompilationUnit = compilationUnit;

            _destinationNamespaceName = Context.ToDestinationNamespaceName(_sourceNamespaceName);
        }

        public void Generate()
        {
            bool hasChildrenProperty = false;
            var collectionProperties = new List<PropertyDeclarationSyntax>();

            var mainClassDescriptors = new Source(Context);
            var mainClassStaticMethods = new Source(Context);
            var mainClassNonstaticMethods = new Source(Context);

            var extensionClassMethods = new Source(Context);
            var attachedClassMethods = new Source(Context);

            // Add the property descriptors and accessors
            foreach (MemberDeclarationSyntax modelObjectMember in Declaration.Members)
            {
                if (!(modelObjectMember is PropertyDeclarationSyntax modelProperty))
                    continue;

                string propertyName = modelProperty.Identifier.Text;
                var property = new Property(Context, this, modelProperty);

#if false
                if (IsCollectionType(modelProperty.Type))
                    collectionProperties.Add(modelProperty);
#endif

                property.GenerateDescriptor(mainClassDescriptors);
                property.GenerateMethods(mainClassNonstaticMethods);
                property.GenerateExtensionClassMethods(extensionClassMethods);

                if (propertyName == "Children")
                    hasChildrenProperty = true;
            }

            // If there are any attached properties, add the property descriptors and accessors for them
            if (AttachedInterfaceDeclaration != null)
            {
                foreach (MemberDeclarationSyntax member in AttachedInterfaceDeclaration.Members)
                {
                    if (!(member is MethodDeclarationSyntax getterMethodDeclaration))
                        continue;

                    // We just process the Get 
                    string methodName = getterMethodDeclaration.Identifier.Text;
                    if (!methodName.StartsWith("Get"))
                    {
                        if (!methodName.StartsWith("Set"))
                            throw new UserViewableException(
                                $"Attached type method {AttachedInterfaceDeclaration.Identifier.Text}.{methodName} doesn't start with Get or Set");
                        else continue;
                    }

                    string propertyName = methodName.Substring("Get".Length);
                    string setterMethodName = "Set" + propertyName;
                    MethodDeclarationSyntax? setterMethodDeclaration = (MethodDeclarationSyntax?)AttachedInterfaceDeclaration.Members.
                        FirstOrDefault(m => m is MethodDeclarationSyntax potentialSetter && potentialSetter.Identifier.Text == setterMethodName);

                    var attachedProperty = new AttachedProperty(Context, this, AttachedInterfaceDeclaration, getterMethodDeclaration, setterMethodDeclaration);

                    attachedProperty.GenerateMainClassDescriptor(mainClassDescriptors);
                    attachedProperty.GenerateMainClassMethods(mainClassStaticMethods);
                    attachedProperty.GenerateAttachedClassMethods(attachedClassMethods);
                }
            }

            Source usingDeclarations = GenerateUsingDeclarations(!mainClassDescriptors.IsEmpty);

            string? destinationBaseClass = GetDestinationBaseClass();

            Source? constructor = GenerateConstructor(collectionProperties);

            string platformOutputDirectory = Context.GetPlatformOutputDirectory(_sourceNamespaceName);

            string mainClassDerviedFrom;
            if (destinationBaseClass == null)
                mainClassDerviedFrom = Name;
            else
                mainClassDerviedFrom = $"{destinationBaseClass}, {Name}";
            Source mainClassSource = GenerateClassFile(usingDeclarations, _destinationNamespaceName, DestinationClassName, mainClassDerviedFrom, constructor, mainClassDescriptors, mainClassStaticMethods, mainClassNonstaticMethods);
            mainClassSource.WriteToFile(platformOutputDirectory, DestinationClassName + ".cs");

            if (AttachedInterfaceDeclaration != null)
            {
                string attachedClassName = DestinationClassName + "Attached";
                string attachedClassDerivedFrom = AttachedInterfaceDeclaration.Identifier.Text;
                Source attachedClassSource = GenerateClassFile(usingDeclarations, _destinationNamespaceName, attachedClassName, attachedClassDerivedFrom, constructor: null, descriptors: null, staticMethods: null, attachedClassMethods);
                attachedClassSource.WriteToFile(platformOutputDirectory, attachedClassName + ".cs");
            }

            if (!extensionClassMethods.IsEmpty)
            {
                string extensionsClassName = DestinationClassName + "Extensions";
                Source extensionsClassSource = GenerateStaticClassFile(GenerateExtensionsClassUsingDeclarations(), _sourceNamespaceName, extensionsClassName, extensionClassMethods);
                extensionsClassSource.WriteToFile(Context.GetSharedOutputDirectory(_sourceNamespaceName), extensionsClassName + ".cs");
            }
        }

        public Source GenerateClassFile(Source usingDeclarations, NameSyntax namespaceName, string className, string derivedFrom, Source? constructor, Source? descriptors, Source? staticMethods, Source? nonstaticMethods)
        {
            Source fileSource = new Source(Context);

            GenerateFileHeader(fileSource);

            if (!usingDeclarations.IsEmpty)
            {
                fileSource.AddSource(usingDeclarations);
                fileSource.AddBlankLine();
            }

            fileSource.AddLines(
                $"namespace {namespaceName}",
                "{");

            using (fileSource.Indent())
            {
                fileSource.AddLines(
                    $"public class {className} : {derivedFrom}",
                    "{");
                using (fileSource.Indent())
                {
                    if (descriptors != null)
                        fileSource.AddSource(descriptors);
                    if (staticMethods != null)
                        fileSource.AddSource(staticMethods);
                    if (constructor != null)
                        fileSource.AddSource(constructor);
                    if (nonstaticMethods != null)
                        fileSource.AddSource(nonstaticMethods);
                }
                fileSource.AddLine(
                    "}");
            }

            fileSource.AddLine(
                "}");

            return fileSource;
        }

        public Source GenerateStaticClassFile(Source usingDeclarations, NameSyntax namespaceName, string className, Source staticMethods)
        {
            Source fileSource = new Source(Context);

            GenerateFileHeader(fileSource);

            if (!usingDeclarations.IsEmpty)
            {
                fileSource.AddSource(usingDeclarations);
                fileSource.AddBlankLine();
            }

            fileSource.AddLines(
                $"namespace {namespaceName}",
                "{");

            using (fileSource.Indent())
            {
                fileSource.AddLines(
                    $"public static class {className}",
                    "{");
                using (fileSource.Indent())
                {
                    fileSource.AddSource(staticMethods);
                }
                fileSource.AddLine(
                    "}");
            }

            fileSource.AddLine(
                "}");

            return fileSource;
        }

        private void GenerateFileHeader(Source fileSource)
        {
            fileSource.AddLine($"// This file is generated from {Name}.cs. Update the source file to change its contents.");
            fileSource.AddBlankLine();
        }

        public OutputType OutputType => Context.OutputType;

        private Source? GenerateConstructor(List<PropertyDeclarationSyntax> collectionProperties)
        {
            if (collectionProperties.Count == 0)
                return null;

            Source constructor = new Source(Context);

            constructor.AddLine($"public {DestinationClassName}()");
            constructor.AddLine("{");

            using (constructor.Indent())
			{
                List<StatementSyntax> statements = new List<StatementSyntax>();
                foreach (PropertyDeclarationSyntax property in collectionProperties)
                {
                    string propertyName = property.Identifier.Text;
                    TypeSyntax destinationPropertyType = Context.ToDestinationType(property.Type);

                    constructor.AddLine($"{propertyName} = new {destinationPropertyType}()");
                }
            }

            constructor.AddLine("}");

            return constructor;
        }

        private Source GenerateUsingDeclarations(bool hasPropertyDescriptors)
        {
            Source source = new Source(Context);

            var usingNames = new Dictionary<string, NameSyntax>();

            foreach (UsingDirectiveSyntax sourceUsing in _sourceCompilationUnit.Usings)
            {
                NameSyntax sourceUsingName = sourceUsing.Name;
                AddUsing(usingNames, sourceUsingName);

                if (sourceUsingName.ToString().StartsWith("Microsoft.StandardUI."))
                    AddUsing(usingNames, Context.ToDestinationNamespaceName(sourceUsingName));
            }

            AddUsing(usingNames, _sourceNamespaceName);

            IEnumerable<QualifiedNameSyntax> outputTypeUsings = OutputType.GetUsings(hasPropertyDescriptors, DestinationTypeHasTypeConverterAttribute());
            foreach (QualifiedNameSyntax outputTypeUsing in outputTypeUsings)
                AddUsing(usingNames, outputTypeUsing);

            foreach (var member in Declaration.Members)
            {
                if (!(member is PropertyDeclarationSyntax modelProperty))
                    continue;

                // Array.Empty requires System
                if (modelProperty.Type is ArrayTypeSyntax)
                    AddUsing(usingNames, IdentifierName("System"));
            }

            if (DestinationTypeHasTypeConverterAttribute())
                AddUsing(usingNames, QualifiedName(OutputType.RootNamespace, IdentifierName("Converters")));

            foreach (NameSyntax name in usingNames.Values)
            {
                source.AddLine($"using {name};");
            }

            return source;
        }

        private Source GenerateExtensionsClassUsingDeclarations()
        {
            Source source = new Source(Context);

            foreach (UsingDirectiveSyntax sourceUsing in _sourceCompilationUnit.Usings)
            {
                source.AddLine($"using {sourceUsing.Name};");
            }

            return source;
        }

        private static void AddUsing(Dictionary<string, NameSyntax> usingNames, NameSyntax name)
        {
            string usingString = name.ToString();
            if (! usingNames.ContainsKey(usingString))
                usingNames.Add(usingString, name);
        }

        private bool DestinationTypeHasTypeConverterAttribute()
        {
            return Context.OutputType is XamlOutputType &&
                   (DestinationClassName == "Geometry" || DestinationClassName == "Brush");
        }

        private string? GetDestinationBaseClass()
        {
            if (Context.IsCollectionType(Declaration.Identifier.Text, out string elementType))
                return $"StandardUICollection<{elementType}>";

            TypeSyntax? baseInterface = Declaration.BaseList?.Types.FirstOrDefault()?.Type;

            if (baseInterface == null)
            {
                if (Declaration.Identifier.Text == "IFrameworkElement")
                    return OutputType.FrameworkElementBaseClassName;
                else return OutputType.DefaultBaseClassName;
            }
            else
                return Context.ToDestinationType(baseInterface).ToString();
        }
    }
}
