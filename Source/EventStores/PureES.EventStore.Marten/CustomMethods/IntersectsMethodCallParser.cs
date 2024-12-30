using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;
using Marten;
using Marten.Linq.Members;
using Marten.Linq.Parsing;
using NpgsqlTypes;
using Weasel.Postgresql.SqlGeneration;

namespace PureES.EventStore.Marten.CustomMethods;

[SuppressMessage("ReSharper", "BitwiseOperatorOnEnumWithoutFlags")]
internal class IntersectsMethodCallParser : IMethodCallParser
{
    public bool Matches(MethodCallExpression expression)
    {
        return expression.Method.DeclaringType == typeof(MartenQueryableExtensions)
            && expression.Method.Name == nameof(MartenQueryableExtensions.Intersects);
    }

    public ISqlFragment? Parse(IQueryableMemberCollection memberCollection, 
        IReadOnlyStoreOptions options,
        MethodCallExpression expression)
    {
        if (expression.Arguments.Count != 2)
            throw new InvalidOperationException();
        if (expression.Arguments[0] is not MemberExpression left)
            throw new InvalidOperationException();
        if (expression.Arguments[1] is not MemberExpression right)
            throw new InvalidOperationException();

        switch (right.Expression)
        {
            case ConstantExpression constant:
                var list = (object?)GetValue(constant) ?? DBNull.Value;
                var property = memberCollection.MemberFor(left).RawLocator;
                var param = new CommandParameter(list, NpgsqlDbType.Array | NpgsqlDbType.Text);
                return new CustomizableWhereFragment($"{property} ?| ^", "^", param);
            default:
                throw new NotImplementedException("Unsupported right expression");
        }
    }

    private static IEnumerable<string>? GetValue(ConstantExpression expression)
    {
        if (expression.Value == null) return null;
        var func = GetValueCache.GetOrAdd(expression.Type, BuildGetValue);
        return func(expression.Value);
    }
    
    private static readonly ConcurrentDictionary<Type,Func<object, IEnumerable<string>?>> GetValueCache = new();

    private static Func<object, IEnumerable<string>?> BuildGetValue(Type type)
    {
        var field = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Single();
        if (!field.FieldType.IsAssignableTo(typeof(IEnumerable<string>)))
            throw new InvalidOperationException();
        var x = Expression.Parameter(typeof(object), "x");
        var body = Expression.Field(Expression.Convert(x, type), field);
        var lambda = Expression.Lambda<Func<object, IEnumerable<string>>>(body, x);
        return lambda.CompileFast();
    }
}