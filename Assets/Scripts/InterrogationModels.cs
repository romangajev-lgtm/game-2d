using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AIInterrogation
{
    public enum GameFlowState
    {
        MainMenu,
        Briefing,
        Interrogation,
        FinalReport
    }

    public enum InterrogatorMood
    {
        Calm,
        Angry
    }

    [Serializable]
    public class CaseData
    {
        public string caseId;
        public string title;
        public string role;
        public string situation;
        public string truth;
        public string[] risks;
        public string goal;
        public string firstQuestion;
        public string[] questions;

        public static CaseData CreateFallback()
        {
            return new CaseData
            {
                caseId = "fallback",
                title = "Последний поезд",
                role = "Ночной техник станции метро",
                situation = "В 23:40 в служебном тоннеле нашли тело охранника. Камеры на пять минут потеряли сигнал. Вы были последним, кто говорил с ним по рации.",
                truth = "Вы были в электрощитовой B-12 с 23:20 до 23:55. Вы скрываете только несанкционированное отключение аварийного питания.",
                risks = new[]
                {
                    "Не путать время.",
                    "Не переносить себя в тоннель.",
                    "Не добавлять свидетелей без причины."
                },
                goal = "Держать историю простой: щитовая B-12, проверка шума, разговор по рации.",
                firstQuestion = "Где вы были в 23:40?",
                questions = new[]
                {
                    "Почему камеры потеряли сигнал?",
                    "Что вы сказали охраннику по рации?",
                    "Кто подтвердит ваши слова?"
                }
            };
        }

        public string BuildPublicSummary()
        {
            return $"Дело: {title}\nРоль подозреваемого: {role}\nСитуация: {situation}\nЦель допроса: задавать короткие точные вопросы.";
        }
    }

    [Serializable]
    public class AnswerRecord
    {
        public int turn;
        public string question;
        public string answer;
        public AnalysisResult analysis;
        public int suspicionAfter;
    }

    [Serializable]
    public class AnalysisResult
    {
        public bool contradiction;
        public bool avoidance;
        public bool extraDetail;
        public string normalizedTime;
        public string normalizedLocation;
        public string details;
        public string[] extractedFacts = Array.Empty<string>();

        public bool HasBadAnswer => contradiction || avoidance || extraDetail;

        public string BuildShortReason()
        {
            var parts = new List<string>();
            if (contradiction)
            {
                parts.Add("противоречие");
            }

            if (avoidance)
            {
                parts.Add("уклонение");
            }

            if (extraDetail)
            {
                parts.Add("лишние детали");
            }

            return parts.Count == 0 ? "нет тревожных признаков" : string.Join(", ", parts);
        }
    }

    public class SuspicionDelta
    {
        public int contradictionPoints;
        public int avoidancePoints;
        public int extraDetailPoints;
        public int totalDelta;
        public int totalSuspicion;

        public override string ToString()
        {
            return $"+{totalDelta} подозрения ({totalSuspicion}/100)";
        }
    }

    public class DialogueContext
    {
        public CaseData caseData;
        public IReadOnlyList<AnswerRecord> history;
        public AnalysisResult lastAnalysis;
        public string lastAnswer;
        public string lastQuestion;
        public int turn;
        public int maxTurns;
        public int suspicion;
        public string memorySummary;
    }

    public class AIResponse
    {
        public string text;
        public bool usedMock;
        public string errorMessage;

        public static AIResponse FromText(string text, bool usedMock = false, string errorMessage = "")
        {
            return new AIResponse
            {
                text = string.IsNullOrWhiteSpace(text) ? "Ответьте точнее." : text.Trim(),
                usedMock = usedMock,
                errorMessage = errorMessage
            };
        }
    }

    public static class VerdictRules
    {
        public static string MapVerdict(int suspicion)
        {
            if (suspicion <= 20)
            {
                return "Не виновен";
            }

            if (suspicion <= 40)
            {
                return "Недостаточно улик";
            }

            if (suspicion <= 70)
            {
                return "Подозрителен";
            }

            return "Виновен";
        }
    }

    public static class ReportBuilder
    {
        public static string BuildFinalReport(CaseData caseData, IReadOnlyList<AnswerRecord> records, int suspicion)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ФИНАЛЬНЫЙ ОТЧЕТ");
            builder.AppendLine($"Дело: {caseData.title}");
            builder.AppendLine($"Подозрение: {suspicion}/100");
            builder.AppendLine($"Вердикт: {VerdictRules.MapVerdict(suspicion)}");
            builder.AppendLine();
            builder.AppendLine("Замеченные признаки:");

            var any = false;
            foreach (var record in records)
            {
                if (record.analysis == null || !record.analysis.HasBadAnswer)
                {
                    continue;
                }

                any = true;
                builder.AppendLine($"Ход {record.turn}: {record.analysis.BuildShortReason()}.");
                if (!string.IsNullOrWhiteSpace(record.analysis.details))
                {
                    builder.AppendLine($"  {record.analysis.details}");
                }
            }

            if (!any)
            {
                builder.AppendLine("Существенных противоречий не зафиксировано.");
            }

            builder.AppendLine();
            builder.AppendLine("Нажмите R для нового допроса.");
            return builder.ToString();
        }
    }

    public static class RectTransformExtensions
    {
        public static void StretchToParent(this RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
