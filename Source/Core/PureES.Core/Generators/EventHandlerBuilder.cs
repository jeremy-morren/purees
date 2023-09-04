using PureES.Core.Generators.Framework;

namespace PureES.Core.Generators;

internal class EventHandlerBuilder
{
    private readonly PureESErrorLogWriter _log;

    private EventHandlerBuilder(IErrorLog log) => _log = new PureESErrorLogWriter(log);
}