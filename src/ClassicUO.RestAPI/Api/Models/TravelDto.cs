using System;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.RestApi.Models
{
    // "travel" object in the player JSON. Built on the game thread (WorldSnapshot.Capture),
    // so it reads TravelState without races.
    internal sealed class TravelDto
    {
        public TravelDto(PlayerMobile player)
        {
            Active = TravelState.Active;
            GoalX = TravelState.GoalX;
            GoalY = TravelState.GoalY;
            GoalZ = TravelState.GoalZ;
            TilesRemaining = Active
                ? Math.Max(Math.Abs(player.X - GoalX), Math.Abs(player.Y - GoalY))
                : 0;
        }

        public bool Active { get; }
        public int GoalX { get; }
        public int GoalY { get; }
        public int GoalZ { get; }
        public int TilesRemaining { get; }
    }

    // Response shape for POST /api/actions/travel.
    internal sealed record TravelResultDto(
        bool Active,
        int GoalX,
        int GoalY,
        int TilesRemaining
    );
}
