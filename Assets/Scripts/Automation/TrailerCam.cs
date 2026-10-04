using LastLight.Sim;
using LastLight.View;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// A directed camera for the trailer shoot: each frame it asks the shot's framing for a pose
    /// and eases the rig towards it. Runs after every Update (so the night has stepped) and before
    /// the rig's own LateUpdate, which still adds shake and the wreck punch on top.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TrailerCam : MonoBehaviour
    {
        CameraRig rig;
        Trailer.Framing framing;
        SimWorld world;
        Vector2 anchor;
        float at;
        bool on, fresh;
        Vector3 pos, look;
        float fov;

        /// <summary>How quickly the camera catches up with the framing (per second).</summary>
        public float Smooth = 2.5f;

        public static TrailerCam Create(CameraRig rig)
        {
            var cam = new GameObject("TrailerCam").AddComponent<TrailerCam>();
            cam.rig = rig;
            return cam;
        }

        /// <summary>Frame a world from now on. The framing's clock reads zero at night time <paramref name="at"/>.</summary>
        public void Begin(Trailer.Framing f, SimWorld w, Vector2 anchor, float at)
        {
            framing = f;
            world = w;
            this.anchor = anchor;
            this.at = at;
            on = true;
            fresh = true;
            rig.SwayAmount = 0f;
        }

        public void Stop()
        {
            on = false;
            rig.SwayAmount = 1f;
        }

        void LateUpdate()
        {
            if (!on || world == null) return;
            var p = framing(world, anchor, world.Time - at);
            if (fresh)
            {
                pos = p.Position;
                look = p.LookAt;
                fov = p.Fov;
                fresh = false;
            }
            else
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * Smooth);
                pos = Vector3.Lerp(pos, p.Position, k);
                look = Vector3.Lerp(look, p.LookAt, k);
                fov = Mathf.Lerp(fov, p.Fov, k);
            }
            rig.Snap(new CameraRig.Pose(pos, look, fov));
        }
    }
}
