using UnityEngine;
using FightCore;

namespace FightGame
{
    //this moves the camera to keep both fighters on screen and does the screen shake
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public float baseSize = 3.4f;
        public float followSpeed = 8f;

        float trauma;
        float shakeTime;
        Vector3 basePos = new Vector3(0f, 2.6f, -10f);
        float punchZoom;
        Vector3 punchTarget;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var rig = go.AddComponent<CameraRig>();
            rig.cam = go.AddComponent<Camera>();
            rig.cam.orthographic = true;
            rig.cam.orthographicSize = rig.baseSize;
            rig.cam.clearFlags = CameraClearFlags.SolidColor;
            rig.cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            rig.cam.nearClipPlane = 0.1f;
            rig.cam.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            go.transform.position = rig.basePos;
            return rig;
        }

        //trauma goes from 0 to 1 and the shake uses trauma squared so small hits are subtle and big ones really rock
        public void Shake(float amount)
        {
            trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));
        }

        //this zooms in a little on a spot for supers and ko hits
        public void Punch(Vector3 worldPos, float amount)
        {
            punchTarget = worldPos;
            punchZoom = Mathf.Max(punchZoom, amount);
        }

        public void Snap(MatchSim sim)
        {
            if (sim == null) return;
            basePos = Target(sim);
            transform.position = basePos;
        }

        Vector3 Target(MatchSim sim)
        {
            var a = sim.fighters[0];
            var b = sim.fighters[1];
            float mid = (a.x + b.x) * 0.5f * FightUtil.SimToWorld;
            float halfW = cam.orthographicSize * cam.aspect;
            float limit = MatchSim.StageHalf * FightUtil.SimToWorld + 0.6f - halfW;
            mid = Mathf.Clamp(mid, -Mathf.Max(0f, limit), Mathf.Max(0f, limit));
            //it only follows jumps a little so the ground stays readable
            float high = Mathf.Max(a.y, b.y) * FightUtil.SimToWorld;
            float y = 2.6f + Mathf.Max(0f, high - 1.5f) * 0.35f;
            return new Vector3(mid, y, -10f);
        }

        public void Tick(MatchSim sim, float dt)
        {
            if (sim != null)
            {
                var target = Target(sim);
                basePos = Vector3.Lerp(basePos, target, 1f - Mathf.Exp(-followSpeed * dt));
            }

            punchZoom = Mathf.MoveTowards(punchZoom, 0f, dt * 1.6f);
            cam.orthographicSize = baseSize * (1f - punchZoom * 0.18f);
            Vector3 pos = Vector3.Lerp(basePos, new Vector3(punchTarget.x, punchTarget.y + 0.5f, -10f), punchZoom * 0.35f);

            shakeTime += dt;
            if (trauma > 0f)
            {
                float s = trauma * trauma;
                float ox = (Mathf.PerlinNoise(shakeTime * 40f, 0.3f) - 0.5f) * 2f * 0.35f * s;
                float oy = (Mathf.PerlinNoise(0.7f, shakeTime * 40f) - 0.5f) * 2f * 0.25f * s;
                pos += new Vector3(ox, oy, 0f);
                trauma = Mathf.Max(0f, trauma - dt * 2.2f);
            }
            transform.position = pos;
        }
    }
}
