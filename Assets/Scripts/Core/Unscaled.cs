namespace LastLight
{
    /// <summary>
    /// Unscaled time for the menus, the radio, audio fades and the camera. Unity's unscaled clock
    /// keeps wall-clock time even while frames are captured at a fixed rate (Time.captureDeltaTime),
    /// so a recording would show those animations at whatever speed the machine rendered. This
    /// clock steps with the captured frames instead, and is Unity's unscaled clock otherwise.
    /// </summary>
    public static class Unscaled
    {
        static int frame = -1;
        static float offset;

        public static float Delta
        {
            get
            {
                Tick();
                return UnityEngine.Time.captureDeltaTime > 0f ? UnityEngine.Time.captureDeltaTime : UnityEngine.Time.unscaledDeltaTime;
            }
        }

        public static float Time
        {
            get
            {
                Tick();
                return UnityEngine.Time.unscaledTime + offset;
            }
        }

        static void Tick()
        {
            if (UnityEngine.Time.frameCount == frame) return;
            frame = UnityEngine.Time.frameCount;
            if (UnityEngine.Time.captureDeltaTime > 0f) offset += UnityEngine.Time.captureDeltaTime - UnityEngine.Time.unscaledDeltaTime;
        }
    }
}
