using PureES.Core.SourceGenerators.Framework;

namespace PureES.Core.SourceGenerators.Generators;

internal abstract class BuilderBase
{
    protected readonly PureESErrorLogWriter Log;

    protected BuilderBase(IErrorLog log)
    {
        Log = new PureESErrorLogWriter(log);
    }

    /// <summary>
    /// Validates that the attribute only occurs on 1 parameter
    /// </summary>
    /// <returns></returns>
    protected bool ValidateSingleAttribute<TAttribute>(IMethod method, out IParameter parameter)
    {
        var list = method.Parameters.Where(p => p.HasAttribute<TAttribute>()).ToList();
        switch (list.Count)
        {
            case 0:
                throw new NotImplementedException();
            case 1:
                parameter = list[0];
                return true;
            default:
                Log.MultipleParametersDefinedWithAttribute(method, typeof(TAttribute));
                parameter = null!;
                return false;
        }
    }

    protected bool ValidateSingleEventEnvelope(IMethod method, out IType? @event)
    {
        @event = null;
        var list = method.Parameters.Where(p => p.Type.IsEventEnvelope()).ToList();
        switch (list.Count)
        {
            case 0:
                throw new NotImplementedException();
            case 1:
                if (list[0].Type.IsGenericEventEnvelope(out var e, out _))
                    @event = e;
                return true;
            default:
                Log.MultipleEventEnvelopeParameters(method);
                return false;
        }
    }

    /// <summary>
    /// Validates that all parameters match a given predicate
    /// </summary>
    protected bool ValidateAllParameters(IMethod method, Func<IParameter, bool> validate)
    {
        var valid = true;
        foreach (var p in method.Parameters)
        {
            //These 2 conditions are always valid
            if (p.HasFromServicesAttribute() || p.Type.IsCancellationToken())
                continue;
            if (validate(p))
                continue;
            Log.UnknownOrDuplicateParameter(method, p);
            valid = false;
        }
        return valid;
    }

    protected static bool ValidateReturnType(IMethod method, IType expected)
    {
        if (method.ReturnType == null) return false;
        if (method.ReturnType.IsAsync(out var underlyingType))
            return underlyingType != null && expected.Equals(underlyingType);
        return method.ReturnType.Equals(expected);
    }
}