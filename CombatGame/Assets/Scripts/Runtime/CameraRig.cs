using UnityEngine;
using FightCore;

namespace FightGame
{
    //this moves the camera to keep both fighters on screen and does the screen shake
    //put it on your camera and change how it feels in the inspector
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("how much of the world fits on screen and smaller is more zoomed in")]
        public float baseSize = 3.4f;
        [Tooltip("the camera height when both fighters are on the ground")]
        public float baseHeight = 2.6f;
        [Tooltip("how fast the camera catches up")]
        public float followSpeed = 8f;
        [Tooltip("how much the camera rises when someone jumps where 0 is not at all")]
        [Range(0f, 1f)] public float jumpFollow = 0.35f;
        [Tooltip("how far past the wall the camera is allowed to see")]
        public float edgePadding = 0.6f;

        [Header("Shake")]
        public float shakeX = 0.35f;
        public float shakeY = 0.25f;
        [Tooltip("how fast shake calms down")]
        public float shakeDecay = 2.2f;
        public float shakeFrequency = 40f;
        [Tooltip("turn this down if screen shake bothers you")]
        [Range(0f, 2f)] public float shakeMultiplier = 1f;

        [Header("Zoom Punch")]
        [Tooltip("how much the camera zooms in for supers and ko hits")]
        [Range(0f, 0.5f)] public float punchZoomAmount = 0.18f;
        public float punchDecay = 1.6f;

        [HideInInspector] public Camera cam;
        float trauma, shakeTime, punchZoom;
        Vector3 basePos, punchTarget;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            basePos = new Vector3(0f, baseHeight, transform.position.z);
        }

        //this is the one the game makes for you if your scene has no camera rig
        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var c = go.AddComponent<Camera>();
            c.orthographic = true;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 2.6f, -10f);
            var rig = go.AddComponent<CameraRig>();
            c.orthographicSize = rig.baseSize;
            return rig;
        }

        //trauma goes from 0 to 1 and the shake uses trauma squared so small hits are subtle and big ones really rock
        public void Shake(float amount)
        {
            trauma = Mathf.Clamp01(Mathf.Max(trauma, amount * shakeMultiplier));
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
            float limit = sim.StageHalf * FightUtil.SimToWorld + edgePadding - halfW;
            mid = Mathf.Clamp(mid, -Mathf.Max(0f, limit), Mathf.Max(0f, limit));
            //it only follows jumps a little so the ground stays readable
            float high = Mathf.Max(a.y, b.y) * FightUtil.SimToWorld;
            float y = baseHeight + Mathf.Max(0f, high - 1.5f) * jumpFollow;
            return new Vector3(mid, y, transform.position.z);
        }

        public void Tick(MatchSim sim, float dt)
        {
            if (sim != null)
                basePos = Vector3.Lerp(basePos, Target(sim), 1f - Mathf.Exp(-followSpeed * dt));

            punchZoom = Mathf.MoveTowards(punchZoom, 0f, dt * punchDecay);
            cam.orthographicSize = baseSize * (1f - punchZoom * punchZoomAmount);
            Vector3 pos = Vector3.Lerp(basePos, new Vector3(punchTarget.x, punchTarget.y + 0.5f, basePos.z), punchZoom * 0.35f);

            shakeTime += dt;
            if (trauma > 0f)
            {
                float s = trauma * trauma;
                float ox = (Mathf.PerlinNoise(shakeTime * shakeFrequency, 0.3f) - 0.5f) * 2f * shakeX * s;
                float oy = (Mathf.PerlinNoise(0.7f, shakeTime * shakeFrequency) - 0.5f) * 2f * shakeY * s;
                pos += new Vector3(ox, oy, 0f);
                trauma = Mathf.Max(0f, trauma - dt * shakeDecay);
            }
            transform.position = pos;
        }
    }
}
