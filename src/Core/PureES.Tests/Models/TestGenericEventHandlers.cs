using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace PureES.Tests.Models;

[PublicAPI]
public class TestGenericEventHandlers<TEvent>
{
    public void On([Event] TEvent created) => throw new NotImplementedException();
    
    public static void On2([Event] TEvent created) => throw new NotImplementedException();
    
    public Task Async([Event] TEvent created) => throw new NotImplementedException();
}

[EventHandlers]
public class ImplementedGenericEventHandlers : TestGenericEventHandlers<Events.Created> {}