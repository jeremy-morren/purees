using Microsoft.AspNetCore.Mvc;

namespace PureES.Core;

/// <summary>
/// Specifies that a record/class is
/// an EventSourcing root aggregate
/// </summary>
/// <remarks>
/// <para>
/// All <c>public static</c> methods where at least one parameter is decorated with
/// the <see cref="CommandAttribute"/> will be treated as command handlers
/// where the return value(s) are event(s). Parameters can
/// be decorated with the <see cref="FromServicesAttribute"/> to inject services.
/// </para>
/// <para>
/// All <c>public static</c> methods which return the parent aggregate type
/// and take an <see cref="EventEnvelope{T,T}"/> parameter
/// will be treated as a <c>When</c> method for aggregate rehydration.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AggregateAttribute : Attribute {}