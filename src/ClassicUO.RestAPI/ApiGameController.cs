using System;
using ClassicUO.Game;
using ClassicUO.RestApi;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;

namespace ClassicUO.RestApi
{
    internal sealed class ApiGameController : GameController
    {
        private readonly ActionQueue _actionQueue;
        private readonly EventBus _eventBus;
        private GameEventHook _hook;

        public ApiGameController(IPluginHost host, ActionQueue queue, EventBus eventBus)
            : base(host)
        {
            _actionQueue = queue;
            _eventBus = eventBus;
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            _hook = new GameEventHook(UO.World, _eventBus);
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            while (_actionQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to execute queued action: {ex}");
                }
            }

            WorldSnapshot.Update(UO.World);
            _hook?.Poll();
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            _hook?.Dispose();
            base.OnExiting(sender, args);
        }
    }
}
