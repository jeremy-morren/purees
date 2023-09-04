using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PureES.Core.Generators.Framework;
using PureES.Core.Generators.Models;
using EventHandler = PureES.Core.Generators.Models.EventHandler;

namespace PureES.Core.Generators;

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
            var log = new CompilationLogProvider(context);

            var aggregateTypes = types
                .Where(t => t.HasAttribute<AggregateAttribute>())
                .Distinct();
            var eventHandlerMethods = types
                .SelectMany(h => h.Methods)
                .Where(m => m.HasAttribute<EventHandlerAttribute>())
                .Distinct();

            try
            {
                var aggregates = new List<Aggregate>();
                string cs;
                string filename;
                foreach (var type in aggregateTypes)
                {
                    if (!PureESTreeBuilder.BuildAggregate(type, out var aggregate, log))
                        continue;
                    aggregates.Add(aggregate);
                    
                    cs = AggregateStoreGenerator.Generate(aggregate, out filename);
                    context.AddSource($"{filename}.g.cs", NormalizeLineEndings(cs));
                    foreach (var handler in aggregate.Handlers)
                    {
                        cs = CommandHandlerGenerator.Generate(aggregate, handler, out filename);
                        context.AddSource($"{filename}.g.cs", cs);
                    }
                }

                var eventHandlers = new List<EventHandler>();

                foreach (var method in eventHandlerMethods)
                {
                    if (!PureESTreeBuilder.BuildEventHandler(method, out var handler, log))
                        continue;
                    eventHandlers.Add(handler);
                }

                var eventHandlerCollections = EventHandlerCollection.Create(eventHandlers).ToList();
                foreach (var collection in eventHandlerCollections)
                {
                    cs = EventHandlerGenerator.Generate(collection, out filename);
                    context.AddSource($"{filename}.g.cs", NormalizeLineEndings(cs));
                }
                
                //Register DI
                cs = DependencyInjectionGenerator.Generate(aggregates, eventHandlerCollections, out filename);
                context.AddSource($"{filename}.g.cs", NormalizeLineEndings(cs));
            }
            catch (Exception e)
            {
                log.WriteError(Location.None, "1000", "Fatal error", "A fatal error has occurred: '{0}'", e.ToString());
            }
        });
    }

    private static string NormalizeLineEndings(string input)
    {
        return input.Replace("\n", Environment.NewLine);
    }
}