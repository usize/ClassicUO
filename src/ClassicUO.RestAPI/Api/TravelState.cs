namespace ClassicUO.RestApi
{
    // Goal state for server-side chunked travel (see ApiGameController.UpdateTravel).
    // Written on the game thread (travel action + travel driver), read by HTTP threads.
    internal static class TravelState
    {
        public static int GoalX;
        public static int GoalY;
        public static sbyte GoalZ;
        public static bool GoalZExplicit;   // z was given in the request (floor matters)
        public static bool Active;
        public static long LastPathfindTicks;   // cooldown bookkeeping
        public static int ConsecutiveFailures;  // grows while every segment attempt fails
    }
}
