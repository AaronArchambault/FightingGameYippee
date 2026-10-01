using System.Diagnostics;
using UnityEditor;
using FightCore;

namespace FightGame.EditorTools
{
    //this runs cpu matches with no graphics to check the sim is still deterministic after you change stuff
    //it plays each match twice plus once as a replay and all three have to end on the exact same checksum
    //if they do not match then something in the sim is using randomness or floats or unity time and that would break replays and online
    public static class SimSelfTest
    {
        [MenuItem("Tools/Fighting Game/Run Determinism Self Test")]
        public static void Run()
        {
            var roster = RosterLoader.Load();
            int checkedCount = 0, fails = 0;
            long frames = 0;
            var sw = Stopwatch.StartNew();
            for (int a = 0; a < roster.Count; a++)
            {
                for (int b = 0; b < roster.Count; b++)
                {
                    EditorUtility.DisplayProgressBar("Determinism Self Test", roster[a].Name + " vs " + roster[b].Name, (a * roster.Count + b) / (float)(roster.Count * roster.Count));
                    var rep = new ReplayData();
                    int len;
                    uint c1 = Play(roster[a], roster[b], 7, null, out len);
                    uint c2 = Play(roster[a], roster[b], 7, rep, out len);
                    uint c3 = Replay(roster[a], roster[b], rep);
                    frames += len * 3;
                    checkedCount++;
                    if (c1 != c2 || c1 != c3)
                    {
                        fails++;
                        UnityEngine.Debug.LogError("Fighting Game self test: " + roster[a].Name + " vs " + roster[b].Name + " is NOT deterministic");
                    }
                }
            }
            EditorUtility.ClearProgressBar();
            sw.Stop();
            string msg = checkedCount + " matchups checked and " + fails + " failed   " + frames + " frames in " + sw.ElapsedMilliseconds + " ms";
            if (fails == 0) UnityEngine.Debug.Log("Fighting Game self test passed   " + msg);
            EditorUtility.DisplayDialog("Determinism Self Test", fails == 0 ? "All good\n\n" + msg : "Something is not deterministic\n\n" + msg, "OK");
        }

        static uint Play(RosterEntry a, RosterEntry b, uint seed, ReplayData rec, out int length)
        {
            var sim = new MatchSim(a.MakeDef(), b.MakeDef(), false);
            var ai1 = new AIBrain(0, AIDifficulty.Hard, seed);
            var ai2 = new AIBrain(1, AIDifficulty.Hard, seed + 100);
            length = 0;
            while (sim.phase != Phase.MatchOver && length < 60 * 60 * 8)
            {
                var i1 = ai1.Decide(sim);
                var i2 = ai2.Decide(sim);
                if (rec != null) rec.Add(i1, i2);
                sim.Tick(i1, i2);
                length++;
            }
            return sim.Checksum();
        }

        static uint Replay(RosterEntry a, RosterEntry b, ReplayData r)
        {
            var sim = new MatchSim(a.MakeDef(), b.MakeDef(), false);
            for (int i = 0; i < r.Length; i++)
                sim.Tick(new FrameInput { bits = (ushort)r.p1Inputs[i] }, new FrameInput { bits = (ushort)r.p2Inputs[i] });
            return sim.Checksum();
        }
    }
}
