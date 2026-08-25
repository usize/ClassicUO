using System;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
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

        // The pathfinder can only search loaded map chunks (~32 tiles around the player),
        // so segments must stay well under that or A* can never reach the waypoint.
        // 25 tiles is the validated working hop size (see item 02).
        private const int TravelSegmentSize = 25;
        private const long TravelPathfindCooldownTicks = 1000;  // base: 1 s between pathfind attempts
        private const long TravelMaxCooldownTicks = 15000;      // backoff cap while failing

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

            UpdateTravel();

            WorldSnapshot.Update(UO.World);
            _hook?.Poll();
        }

        // Server-side chunked travel: each tick, if a travel goal is active and the current
        // segment is not auto-walking (and the cooldown has elapsed), pathfind the next
        // segment (≤TravelSegmentSize tiles) toward the goal. Unreachable segments are
        // retried with exponential backoff (1 s, 2 s, 4 s, 8 s, 15 s cap) — the goal is
        // never cleared (obstacles may move) but pathfinding is never hot-looped, so a
        // permanently blocked goal settles to near-zero CPU.
        private void UpdateTravel()
        {
            if (!TravelState.Active)
            {
                return;
            }

            var world = UO.World;
            var player = world?.Player;
            if (player == null || !world.InGame || player.IsDead || player.IsParalyzed)
            {
                return;
            }

            if (world.TargetManager?.IsTargeting ?? false)
            {
                return;
            }

            int goalX = TravelState.GoalX;
            int goalY = TravelState.GoalY;
            int goalZ = TravelState.GoalZ;
            int dx = Math.Abs(player.X - goalX);
            int dy = Math.Abs(player.Y - goalY);

            // Arrived: within 1 tile (x/y), and on the goal's floor when a Z was requested.
            // Floors are ~20 Z units apart, so a 2-unit tolerance never spans floors but
            // absorbs stair/bridge wobble. When Z was omitted the caller wants the x/y spot
            // on whatever floor it reaches (ground can slope ±3 between distant tiles).
            bool onFloor = !TravelState.GoalZExplicit || Math.Abs(player.Z - goalZ) <= 2;
            if (Math.Max(dx, dy) <= 1 && onFloor)
            {
                TravelState.Active = false;
                if (player.Pathfinder.AutoWalking)
                {
                    player.Pathfinder.StopAutoWalk();
                }
                return;
            }

            if (player.Pathfinder.AutoWalking)
            {
                return; // current segment still walking
            }

            // Backoff while failing: 1 s, 2 s, 4 s, 8 s, then 15 s cap. Reset on any
            // successful pathfind (see TryPathfindSegment), so a goal blocked by a moving
            // obstacle is retried quickly at first.
            long cooldown = TravelPathfindCooldownTicks << Math.Min(TravelState.ConsecutiveFailures, 4);
            if (cooldown > TravelMaxCooldownTicks)
            {
                cooldown = TravelMaxCooldownTicks;
            }

            if (Time.Ticks - TravelState.LastPathfindTicks < cooldown)
            {
                return; // re-pathfind cooldown
            }

            // Waypoint: if the goal is farther than one segment, walk to a point
            // TravelSegmentSize tiles along the straight line toward the goal (measured
            // from the player, staying on the player's floor); otherwise walk straight
            // to the goal.
            int waypointX, waypointY;
            sbyte waypointZ;
            if (Math.Max(dx, dy) <= TravelSegmentSize)
            {
                waypointX = goalX;
                waypointY = goalY;
                waypointZ = TravelState.GoalZ;
            }
            else
            {
                waypointX = player.X + Math.Clamp(goalX - player.X, -TravelSegmentSize, TravelSegmentSize);
                waypointY = player.Y + Math.Clamp(goalY - player.Y, -TravelSegmentSize, TravelSegmentSize);
                waypointZ = player.Z;
            }

            if (TryPathfindSegment(player, waypointX, waypointY, waypointZ))
            {
                return;
            }

            // No path on any candidate: keep the goal, back off, retry later
            // (obstacles may move; never hot-loop pathfinding).
            TravelState.LastPathfindTicks = Time.Ticks;
            TravelState.ConsecutiveFailures++;
        }

        private static bool TryPathfindSegment(PlayerMobile player, int waypointX, int waypointY, sbyte waypointZ)
        {
            if (player.Pathfinder.WalkTo(waypointX, waypointY, waypointZ, 0) > 0)
            {
                TravelState.LastPathfindTicks = Time.Ticks;
                TravelState.ConsecutiveFailures = 0;
                return true;
            }

            // Up to 5 alternative waypoints: the same step offset perpendicular to the
            // travel line by ±5 and ±10 tiles (clamped to ≥0).
            int dirX = waypointX - player.X;
            int dirY = waypointY - player.Y;
            double length = Math.Sqrt((double)dirX * dirX + (double)dirY * dirY);
            if (length < 1e-6)
            {
                return false;
            }

            double px = -(double)dirY / length;
            double py = (double)dirX / length;

            foreach (int offset in new[] { 5, -5, 10, -10 })
            {
                int ox = Math.Max(0, (int)Math.Round(waypointX + px * offset));
                int oy = Math.Max(0, (int)Math.Round(waypointY + py * offset));
                if (player.Pathfinder.WalkTo(ox, oy, waypointZ, 0) > 0)
                {
                    TravelState.LastPathfindTicks = Time.Ticks;
                    TravelState.ConsecutiveFailures = 0;
                    return true;
                }
            }

            return false;
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            _hook?.Dispose();
            base.OnExiting(sender, args);
        }
    }
}
