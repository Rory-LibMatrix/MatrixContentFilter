using System.Diagnostics;
using LibMatrix;
using LibMatrix.Responses;
using MatrixContentFilter.EventTypes;

namespace MatrixContentFilter.Abstractions;

public abstract class IContentFilter
{
    public virtual Task ProcessSyncAsync(SyncResponse syncEvent) {
        var type = this.GetType().FullName;
        Console.WriteLine($"WARNING: {type} does not implement ProcessSyncAsync(SyncResponse syncEvent)");
        if(Debugger.IsAttached)
            Debugger.Break();
        return Task.CompletedTask;
    }

    public virtual Task ProcessEventListAsync(List<StateEventResponse> events) {
        var type = this.GetType().FullName;
        Console.WriteLine($"WARNING: {type} does not implement ProcessEventListAsync(List<StateEventResponse> events)");
        if(Debugger.IsAttached)
            Debugger.Break();
        return Task.CompletedTask;
    }

    public int ActionCount { get; set; } = 0;
}