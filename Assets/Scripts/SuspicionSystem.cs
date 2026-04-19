using UnityEngine;

namespace AIInterrogation
{
    public class SuspicionSystem : MonoBehaviour
    {
        public const int ContradictionPoints = 15;
        public const int AvoidancePoints = 5;
        public const int ExtraDetailPoints = 5;

        public int Suspicion { get; private set; }

        public void ResetSuspicion()
        {
            Suspicion = 0;
        }

        public void SetSuspicion(int value)
        {
            Suspicion = Mathf.Clamp(value, 0, 100);
        }

        public int AddSuspicion(int delta)
        {
            Suspicion = Mathf.Clamp(Suspicion + delta, 0, 100);
            return Suspicion;
        }

        public SuspicionDelta Apply(AnalysisResult result)
        {
            var delta = new SuspicionDelta();
            if (result != null && result.contradiction)
            {
                delta.contradictionPoints = ContradictionPoints;
            }

            if (result != null && result.avoidance)
            {
                delta.avoidancePoints = AvoidancePoints;
            }

            if (result != null && result.extraDetail)
            {
                delta.extraDetailPoints = ExtraDetailPoints;
            }

            delta.totalDelta = delta.contradictionPoints + delta.avoidancePoints + delta.extraDetailPoints;
            Suspicion = Mathf.Clamp(Suspicion + delta.totalDelta, 0, 100);
            delta.totalSuspicion = Suspicion;
            return delta;
        }

        public InterrogatorMood ResolveMood(AnalysisResult lastAnalysis)
        {
            if (Suspicion >= 70)
            {
                return InterrogatorMood.Angry;
            }

            if (Suspicion >= 40 && lastAnalysis != null && lastAnalysis.contradiction)
            {
                return InterrogatorMood.Angry;
            }

            return InterrogatorMood.Calm;
        }
    }
}
