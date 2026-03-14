using System;
using System.Collections.Concurrent;

namespace ClassicUO.RestApi
{
    public class ActionQueue
    {
        private readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        public void Enqueue(Action action)
        {
            _actions.Enqueue(action);
        }

        public bool TryDequeue(out Action action)
        {
            return _actions.TryDequeue(out action);
        }
    }
}
