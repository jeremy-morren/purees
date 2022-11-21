﻿using System.Reflection;

namespace PureES.Core.ExpBuilders.WhenHandlers;

internal static class ValueTaskHelpers
{
    public static readonly MethodInfo FromTaskMethod = typeof(ValueTaskHelpers).GetStaticMethod(nameof(FromTask));

    public static readonly MethodInfo
        FromResultMethod = typeof(ValueTask).GetStaticMethod(nameof(ValueTask.FromResult));

    public static readonly MethodInfo DefaultMethod = typeof(ValueTaskHelpers).GetStaticMethod(nameof(Default));

    public static ValueTask<T?> Default<T>() => ValueTask.FromResult<T?>(default);

    public static async ValueTask<T> FromTask<T>(Task<T> source)
    {
        await source;
        return source.Result;
    }
}