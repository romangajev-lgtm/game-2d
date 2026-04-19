using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AIInterrogation
{
    public class MemorySystem : MonoBehaviour
    {
        private readonly List<AnswerRecord> records = new List<AnswerRecord>();
        private readonly HashSet<string> facts = new HashSet<string>();
        private string firstKnownTime;
        private string firstKnownLocation;

        public IReadOnlyList<AnswerRecord> Records => records;
        public string FirstKnownTime => firstKnownTime;
        public string FirstKnownLocation => firstKnownLocation;

        public void ResetMemory()
        {
            records.Clear();
            facts.Clear();
            firstKnownTime = string.Empty;
            firstKnownLocation = string.Empty;
        }

        public bool HasFact(string fact)
        {
            return !string.IsNullOrWhiteSpace(fact) && facts.Contains(fact);
        }

        public void Remember(int turn, string question, string answer, AnalysisResult analysis, int suspicionAfter)
        {
            if (analysis == null)
            {
                analysis = new AnalysisResult();
            }

            var record = new AnswerRecord
            {
                turn = turn,
                question = question,
                answer = answer,
                analysis = analysis,
                suspicionAfter = suspicionAfter
            };

            records.Add(record);

            if (!string.IsNullOrWhiteSpace(analysis.normalizedTime) && string.IsNullOrWhiteSpace(firstKnownTime))
            {
                firstKnownTime = analysis.normalizedTime;
            }

            if (!string.IsNullOrWhiteSpace(analysis.normalizedLocation) && string.IsNullOrWhiteSpace(firstKnownLocation))
            {
                firstKnownLocation = analysis.normalizedLocation;
            }

            foreach (var fact in analysis.extractedFacts ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(fact))
                {
                    facts.Add(fact);
                }
            }
        }

        public string BuildSummary()
        {
            if (records.Count == 0)
            {
                return "Пока нет ответов.";
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(firstKnownTime))
            {
                builder.Append("Первое заявленное время: ").Append(firstKnownTime).Append(". ");
            }

            if (!string.IsNullOrWhiteSpace(firstKnownLocation))
            {
                builder.Append("Первое заявленное место: ").Append(firstKnownLocation).Append(". ");
            }

            builder.Append("Факты: ");
            builder.Append(facts.Count == 0 ? "нет устойчивых фактов" : string.Join(", ", facts.Take(8)));
            return builder.ToString();
        }
    }
}
