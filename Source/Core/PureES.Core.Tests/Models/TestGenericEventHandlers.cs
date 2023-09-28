using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace PureES.Core.Tests.Models;

[PublicAPI]
public class TestGenericEventHandlers<TEvent>
{
    [EventHandler]
    public void On([Event] TEvent created) => throw new NotImplementedException();
    
    [EventHandler]
    public static void On2([Event] TEvent created) => throw new NotImplementedException();
    
    [EventHandler]
    public Task Async([Event] TEvent created) => throw new NotImplementedException();
}

public class ImplementedGenericEventHandlers : TestGenericEventHandlers<Events.Created> {}