using UnityEngine;

namespace LastLight.Sim
{
    public enum ShipType { Trawler, Steamer, Ferry }

    public enum ShipState { Sailing, Lost, Lured, Arrived, Wrecked }

    public sealed class ShipStats
    {
        public float Radius, Length, Speed, TurnRate, DrainTime, Lookahead, WaypointRadius;
        public int Points;
        public bool DeepDraught;

        public static readonly ShipStats Trawler = new ShipStats
        {
            Radius = 2.2f, Length = 8f, Speed = 6.6f, TurnRate = 72f, DrainTime = 8f, Lookahead = 15f,
            WaypointRadius = 9f, Points = 100,
        };

        public static readonly ShipStats Steamer = new ShipStats
        {
            Radius = 3.8f, Length = 16f, Speed = 4.4f, TurnRate = 24f, DrainTime = 15f, Lookahead = 30f,
            WaypointRadius = 14f, Points = 150, DeepDraught = true,
        };

        public static readonly ShipStats Ferry = new ShipStats
        {
            Radius = 3.2f, Length = 13f, Speed = 5.5f, TurnRate = 40f, DrainTime = 11f, Lookahead = 21f,
            WaypointRadius = 11f, Points = 250,
        };

        public static ShipStats For(ShipType t) => t switch
        {
            ShipType.Steamer => Steamer,
            ShipType.Ferry => Ferry,
            _ => Trawler,
        };

        public static ShipType Parse(string s) => s switch
        {
            "steamer" => ShipType.Steamer,
            "ferry" => ShipType.Ferry,
            _ => ShipType.Trawler,
        };
    }

    public sealed class SimShip
    {
        public int Id;
        public ShipType Type;
        public ShipStats Stats;
        public string Name, Captain;
        public bool Damaged;
        public Route Route;
        public int Waypoint = 1;

        public Vector2 Pos;
        public float Heading;
        public float Speed;
        public Vector2 Velocity;
        public float Confidence = 1f;
        public ShipState State = ShipState.Sailing;

        public float Light;            // true-beam intensity at the hull this step
        public bool Lit, InAura, InFog, Inside, EverInside;
        public bool EverLost, EverLured;
        public SimWrecker LuredBy;
        public float Age, TimeSinceLit, FlareTimer = 6f;
        public int AvoidSide;          // sticky tangent choice while avoiding (-1, 0, +1)
        /// <summary>Reefs/shoals this captain has seen charted near their course; remembered until passed.</summary>
        public readonly System.Collections.Generic.HashSet<int> KnownReefs = new System.Collections.Generic.HashSet<int>();
        public readonly System.Collections.Generic.HashSet<int> KnownShoals = new System.Collections.Generic.HashSet<int>();
        public int AvoidObstacle = -1;
        public string SteerDebug = "";
        public string WreckCause = "";
        // For the dawn debrief: how often the captain lost the way or was lured, and how the wreck happened.
        public int LostCount, LuredCount;
        public string LuredAt = "";            // the wrecker site that last lured this ship
        public ShipState WreckedWhile;         // Sailing, Lost or Lured when the hull struck
        public bool WreckCharted;              // the hazard was charted (or always visible) when struck
        public bool WreckShoal;                // ran aground on a sandbank rather than striking rock
        public float WreckTime = -1f;
        public float StateTime;        // seconds in the current state

        public bool Active => State == ShipState.Sailing || State == ShipState.Lost || State == ShipState.Lured;
        public bool Resolved => State == ShipState.Arrived || State == ShipState.Wrecked;
        public Vector2 Forward => Geo.Dir(Heading);
        public bool SteadyHand => !EverLost && !EverLured;
    }

    public sealed class SimReef
    {
        public int Index;
        public string Id, Group;
        public Vector2 Pos;
        public float Radius;
        public float Exposure;         // seconds of light accumulated towards charting
        public float ChartTimer;       // > 0 while charted
        public float Light;
        public bool Charted => ChartTimer > 0f;

        public const float ChartDuration = 22f;
        public const float ExposureNeeded = 0.3f;
    }

    public sealed class SimShoal
    {
        public Shoal Def;
        public Vector2[] Axis;
        public float Exposure, ChartTimer, Light;
        public bool Charted => ChartTimer > 0f;
    }

    public sealed class SimBuoy
    {
        public int Index;
        public string Id, Name, Kind;
        public Vector2 Pos;
        public float Charge;           // 1 = freshly lit, burns down to 0
        public float Light;
        public bool Burning => Charge > 0f;

        public const float BurnTime = 28f;
        public const float AuraRadius = 20f;
    }

    public sealed class SimFogBank
    {
        public Vector2 Pos, Velocity;
        public float Radius, Density;
    }

    public enum WreckerState { Waiting, Burning, Doused }

    public sealed class SimWrecker
    {
        public int Index;
        public WreckerDef Def;
        public WreckerSite[] Sites;
        public int SiteIndex;
        public WreckerState State = WreckerState.Waiting;
        public float Timer;            // waiting / doused countdown
        public float Bearing;          // radians
        public int SweepDir = 1;
        public float DouseProgress;    // 0..1 while the true beam holds the lantern
        public float Flicker;

        public WreckerSite Site => Sites[SiteIndex];
        public bool Burning => State == WreckerState.Burning;

        public const float HalfAngle = 10f;
        public const float Range = 78f;
    }
}
