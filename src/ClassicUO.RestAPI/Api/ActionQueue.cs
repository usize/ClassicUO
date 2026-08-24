using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ClassicUO.RestApi
{
    public class ActionQueue
    {
        private readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        public void Enqueue(Action action)
        {
            _actions.Enqueue(action);
        }

        public Task<T> EnqueueAwait<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(() =>
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        public bool TryDequeue(out Action action)
        {
            return _actions.TryDequeue(out action);
        }
    }
}
