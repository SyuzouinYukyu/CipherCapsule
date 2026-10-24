namespace CipherCapsule.Builder;

public sealed record EditorSwitchDraft(bool Approved,string Html);
public sealed record EditorSwitchTrace(string Phase,int Target,long Revision,string? ExceptionType=null,int? HResult=null);

// Called on the owning UI synchronization context. Preparation is bounded and may not commit UI state.
// Obsolete preparations may finish later, but only the latest successful request can commit.
public sealed class EditorSwitchQueue(Func<int> current,Func<int,CancellationToken,Task<EditorSwitchDraft>> prepare,Action<int,EditorSwitchDraft> commit,Action<bool> busy,Action<Exception> failure,Action<EditorSwitchTrace>? trace=null,TimeSpan? timeout=null)
{
    readonly TimeSpan limit=timeout??TimeSpan.FromSeconds(8);
    TaskCompletionSource? running;CancellationTokenSource? active;int desired;long revision;bool stopped;
    public bool IsBusy=>running!=null;
    public Task Idle=>running?.Task??Task.CompletedTask;
    public Task Request(int target)
    {
        if(stopped||target is <0 or >1)return Task.CompletedTask;
        if(running!=null&&target==desired)return running.Task;
        if(running==null&&target==current())return Task.CompletedTask;
        desired=target;revision++;active?.Cancel();trace?.Invoke(new("request",target,revision));
        if(running!=null)return running.Task;
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);running=completion;_=Drain(completion);return completion.Task;
    }
    async Task Drain(TaskCompletionSource completion)
    {
        try
        {
            busy(true);
            while(!stopped&&desired!=current())
            {
                int target=desired;long stamp=revision;active=new();
                try
                {
                    trace?.Invoke(new("start",target,stamp));
                    Task<EditorSwitchDraft> operation=prepare(target,active.Token);
                    _=operation.ContinueWith(t=>{_ = t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                    var draft=await operation.WaitAsync(limit,active.Token);
                    if(stopped||stamp!=revision)continue;
                    if(!draft.Approved){desired=current();trace?.Invoke(new("declined",target,stamp));break;}
                    commit(target,draft);trace?.Invoke(new("complete",target,stamp));
                }
                catch(OperationCanceledException) when(stopped||stamp!=revision){trace?.Invoke(new("superseded",target,stamp));}
                catch(Exception error)
                {
                    active.Cancel();trace?.Invoke(new("failure",target,stamp,error.GetType().Name,error.HResult));
                    if(stopped||stamp!=revision)continue;
                    desired=current();failure(error);break; // explicit user retry, no automatic retry loop
                }
                finally{active.Dispose();active=null;}
            }
        }
        catch(Exception error){desired=current();failure(error);}
        finally
        {
            try{busy(false);}finally{running=null;completion.TrySetResult();}
        }
    }
    public void Stop(){stopped=true;revision++;active?.Cancel();}
}
