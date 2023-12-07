using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PureES.SourceGenerators.Framework;
using PureES.SourceGenerators.Models;

namespace PureES.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class PureESIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //We run on all changes of 'Type' or 'Constructor'

        var types = context.SyntaxProvider.CreateSyntaxProvider(
                static (n, _) => n is TypeDeclarationSyntax or MethodDeclarationSyntax,
                static (n, _) =>
                {
                    return n.Node switch
                    {
                        TypeDeclarationSyntax t => new TypeSymbol(
                            (ITypeSymbol)n.SemanticModel.GetDeclaredSymbol(t)!,
                            t.IsPartial()),
                        MethodDeclarationSyntax { Parent: TypeDeclarationSyntax mt } => new TypeSymbol(
                            (ITypeSymbol)n.SemanticModel.GetDeclaredSymbol(mt)!, mt.IsPartial()),
                        _ => null!
                    };
                })
            .Where(t => t != null!);
        
        context.RegisterSourceOutput(types.Collect(), static (context, types) =>
        {
            void AddSource(string filename, string cs)
            {
                cs = cs.Replace("\n", Environment.NewLine); //Fix line endings
                filename = TypeNameHelpers.SanitizeFilename(filename);
                context.AddSource($"{filename}.g.cs", cs);
            }
            
            var log = new CompilationLogProvider(context);

            var aggregateTypes = types
                .Where(t => t.HasAggregateAttribute())
                .Distinct();
            
            var eventHandlerTypes = types
                .Where(t => t.HasEventHandlersAttribute())
                .Distinct();

            try
            {
                var aggregates = new List<Aggregate>();
                string cs;
                string filename;
                foreach (var type in aggregateTypes)
                {
                    if (!AggregateBuilder.BuildAggregate(type, out var aggregate, log))
                        continue;
                    aggregates.Add(aggregate);

                    cs = AggregateFactoryGenerator.Generate(aggregate, out filename);
                    AddSource(filename, cs);
                    foreach (var handler in aggregate.Handlers)
                    {
                        cs = CommandHandlerGenerator.Generate(aggregate, handler, out filename);
                        AddSource(filename, cs);
                    }
                }

                var eventHandlers = new List<Models.EventHandler>();

                foreach (var type in eventHandlerTypes)
                {
                    if (!EventHandlersBuilder.BuildEventHandlers(type, out var handlers, log))
                        continue;
                    eventHandlers.AddRange(handlers);
                }

                foreach (var handler in eventHandlers)
                {
                    cs = EventHandlerGenerator.Generate(handler, out filename);
                    AddSource(filename, cs);
                }

                //Register DI
                cs = DependencyInjectionGenerator.Generate(aggregates, eventHandlers, out filename);
                AddSource(filename, cs);
            }
            catch (Exception e)
            {
                var str = e.ToString().Replace(Environment.NewLine, "\\n");
                log.WriteError(Location.None,
                    "1000",
                    "Fatal error",
                    "Assembly location: '{0}'. A fatal error has occurred: '{1}'",
                    typeof(PureESIncrementalGenerator).Assembly.Location, str);
            }
        });
    }
}