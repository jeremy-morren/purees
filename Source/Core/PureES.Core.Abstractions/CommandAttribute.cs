﻿namespace PureES.Core;

/// <summary>
///     Specifies that a method parameter should
///     be bound using the parameter from <see cref="CommandHandler{T}" />
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CommandAttribute : Attribute
{
}