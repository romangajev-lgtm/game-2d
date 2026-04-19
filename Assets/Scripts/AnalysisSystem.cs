using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AIInterrogation
{
    public class AnalysisSystem : MonoBehaviour
    {
        private static readonly Regex ClockRegex = new Regex(@"\b([01]?\d|2[0-3])[:.]([0-5]\d)\b", RegexOptions.Compiled);
        private static readonly Regex HourRegex = new Regex(@"\b([01]?\d|2[0-3])\s*(час(?:а|ов)?|ч\.?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LocationRegex = new Regex(@"\b(?:в|во|на|у|около|возле)\s+([а-яёa-z0-9\- ]{3,34})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] AvoidanceMarkers =
        {
            "не помню",
            "не знаю",
            "без комментариев",
            "какая разница",
            "не обязан",
            "не хочу говорить",
            "не уверен",
            "кажется",
            "вроде",
            "может быть",
            "не спрашивайте"
        };

        private static readonly string[] DetailMarkers =
        {
            "потом",
            "затем",
            "кстати",
            "еще",
            "между прочим",
            "почему-то",
            "случайно",
            "незнаком",
            "свидетель",
            "нож",
            "кров",
            "ключ",
            "пистолет",
            "деньги"
        };

        public AnalysisResult Analyze(string answer, MemorySystem memory, CaseData caseData, string question)
        {
            var normalizedAnswer = (answer ?? string.Empty).Trim();
            var lower = normalizedAnswer.ToLowerInvariant();
            var result = new AnalysisResult();
            var facts = new List<string>();
            var detailReasons = new List<string>();

            result.normalizedTime = NormalizeTime(lower);
            result.normalizedLocation = NormalizeLocation(lower);

            if (!string.IsNullOrWhiteSpace(result.normalizedTime))
            {
                facts.Add("time:" + result.normalizedTime);
            }

            if (!string.IsNullOrWhiteSpace(result.normalizedLocation))
            {
                facts.Add("location:" + result.normalizedLocation);
            }

            result.avoidance = AvoidanceMarkers.Any(lower.Contains) || IsSuspiciouslyShort(lower);

            var contradictionReasons = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.normalizedTime) &&
                !string.IsNullOrWhiteSpace(memory.FirstKnownTime) &&
                result.normalizedTime != memory.FirstKnownTime)
            {
                result.contradiction = true;
                contradictionReasons.Add($"время изменилось с {memory.FirstKnownTime} на {result.normalizedTime}");
            }

            if (!string.IsNullOrWhiteSpace(result.normalizedLocation) &&
                !string.IsNullOrWhiteSpace(memory.FirstKnownLocation) &&
                !AreLocationsCompatible(memory.FirstKnownLocation, result.normalizedLocation))
            {
                result.contradiction = true;
                contradictionReasons.Add($"место изменилось с \"{memory.FirstKnownLocation}\" на \"{result.normalizedLocation}\"");
            }

            AddTruthMismatchHints(result, caseData, lower, contradictionReasons);

            var newFactCount = facts.Count(fact => !memory.HasFact(fact));
            var wordCount = lower.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var markedDetails = DetailMarkers.Where(lower.Contains).ToArray();
            if (newFactCount >= 2 && memory.Records.Count > 0)
            {
                result.extraDetail = true;
                detailReasons.Add("появилось несколько новых фактов");
            }

            if (wordCount > 34 || markedDetails.Length >= 2)
            {
                result.extraDetail = true;
                detailReasons.Add("ответ перегружен деталями");
            }

            result.extractedFacts = facts.ToArray();
            result.details = BuildDetails(contradictionReasons, detailReasons, result.avoidance);
            return result;
        }

        public string NormalizeTime(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var clockMatch = ClockRegex.Match(source);
            if (clockMatch.Success)
            {
                var hour = int.Parse(clockMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var minute = int.Parse(clockMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                return $"{hour:00}:{minute:00}";
            }

            var hourMatch = HourRegex.Match(source);
            if (hourMatch.Success)
            {
                var hour = int.Parse(hourMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                return $"{hour:00}:00";
            }

            if (source.Contains("полноч"))
            {
                return "00:00";
            }

            if (source.Contains("утром"))
            {
                return "утро";
            }

            if (source.Contains("днем") || source.Contains("днём"))
            {
                return "день";
            }

            if (source.Contains("вечером"))
            {
                return "вечер";
            }

            if (source.Contains("ночью"))
            {
                return "ночь";
            }

            return string.Empty;
        }

        public string NormalizeLocation(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var match = LocationRegex.Match(source);
            if (!match.Success)
            {
                return string.Empty;
            }

            var location = match.Groups[1].Value.Trim().ToLowerInvariant();
            location = Regex.Replace(location, @"[,.!?;:]+$", string.Empty);
            location = Regex.Replace(location, @"\s+", " ");

            var stopWords = new[] { "я", "мы", "меня", "нас", "это", "тот", "когда", "который" };
            var words = location.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .TakeWhile(word => !stopWords.Contains(word))
                .Take(4);
            return string.Join(" ", words).Trim();
        }

        private static bool IsSuspiciouslyShort(string answer)
        {
            var trimmed = answer.Trim();
            return trimmed.Length > 0 && trimmed.Length < 5;
        }

        private static bool AreLocationsCompatible(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return a.Contains(b) || b.Contains(a);
        }

        private static void AddTruthMismatchHints(AnalysisResult result, CaseData caseData, string lower, List<string> reasons)
        {
            if (caseData == null || string.IsNullOrWhiteSpace(caseData.truth))
            {
                return;
            }

            var truth = caseData.truth.ToLowerInvariant();
            var truthMentionsPanel = truth.Contains("щитовой") || truth.Contains("электрощитовой");
            var truthMentionsTunnel = truth.Contains("тоннел");

            if (truthMentionsPanel && lower.Contains("тоннел") && !truthMentionsTunnel)
            {
                result.contradiction = true;
                reasons.Add("ответ переносит вас в тоннель, хотя правда держит вас у щитовой");
            }
        }

        private static string BuildDetails(List<string> contradictionReasons, List<string> detailReasons, bool avoidance)
        {
            var parts = new List<string>();
            parts.AddRange(contradictionReasons);
            parts.AddRange(detailReasons);
            if (avoidance)
            {
                parts.Add("ответ звучит уклончиво");
            }

            return string.Join("; ", parts);
        }
    }
}
