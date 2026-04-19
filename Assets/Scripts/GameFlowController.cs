using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AIInterrogation
{
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] private int maxTurns = 12;
        [SerializeField] private int startingSuspicion = 10;
        [SerializeField] private int aggressionSuspicionDelta = 10;
        [SerializeField] private int firstGuaranteedTableSlamAnswer = 4;
        [SerializeField] private int minTableSlamInterval = 2;
        [SerializeField] private int maxTableSlamInterval = 7;
        [SerializeField, Range(0f, 1f)] private float tableSlamChance = 0.28f;
        [SerializeField] private float tableSlamReturnDelay = 1.5f;

        private MemorySystem memorySystem;
        private AnalysisSystem analysisSystem;
        private SuspicionSystem suspicionSystem;
        private DialogueManager dialogueManager;
        private InterrogationUIController ui;
        private SceneAtmosphereController sceneAtmosphere;
        private AudioController audioController;
        private CaseData caseData;
        private CancellationTokenSource cancellation;
        private string currentQuestion;
        private int currentTurn;
        private int lastTableSlamAnswer = -1;
        private bool initialized;
        private bool busy;

        public GameFlowState State { get; private set; } = GameFlowState.MainMenu;
        public bool CanSubmitAnswer => State == GameFlowState.Interrogation && !busy;
        public int MaxTurns => maxTurns;

        public void InitializeRuntime()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            memorySystem = GetOrAdd<MemorySystem>();
            analysisSystem = GetOrAdd<AnalysisSystem>();
            suspicionSystem = GetOrAdd<SuspicionSystem>();
            dialogueManager = GetOrAdd<DialogueManager>();
            ui = GetOrAdd<InterrogationUIController>();
            sceneAtmosphere = GetOrAdd<SceneAtmosphereController>();
            audioController = GetOrAdd<AudioController>();

            caseData = LoadCase();
            audioController.Initialize();
            sceneAtmosphere.Initialize();
            ui.Initialize(this, audioController);
            dialogueManager.Initialize();
            ShowMainMenu();
        }

        public void NewCaseFromMenu()
        {
            audioController.PlaySubmit();
            ShowBriefing();
        }

        public void LoadGameFromMenu()
        {
            audioController.PlayTerminalBeep();
            ui.SetMenuMessage("Сохранений пока нет. Начните новое дело.");
        }

        public void ExitFromMenu()
        {
            audioController.PlaySubmit();
            Application.Quit();
            ui.SetMenuMessage("Выход из игры...");
        }

        public void StartInterrogation()
        {
            StopAllCoroutines();
            cancellation?.Cancel();
            cancellation = new CancellationTokenSource();

            currentTurn = 0;
            lastTableSlamAnswer = -1;
            busy = false;
            State = GameFlowState.Interrogation;
            memorySystem.ResetMemory();
            suspicionSystem.ResetSuspicion();
            suspicionSystem.SetSuspicion(startingSuspicion);
            dialogueManager.Initialize();
            sceneAtmosphere.ForceMood(InterrogatorMood.Calm);
            ui.ShowInterrogation();
            ui.SetSuspicion(suspicionSystem.Suspicion);
            ui.SetStatus("REC");
            var startupNotice = dialogueManager.ConsumeStartupNotice();
            if (!string.IsNullOrWhiteSpace(startupNotice))
            {
                ui.AppendSystem(startupNotice);
            }

            ui.AppendSystem("Для осмотра комнаты напишите: я хочу посмотреть улики");

            currentQuestion = string.IsNullOrWhiteSpace(caseData.firstQuestion)
                ? "Где вы были в момент происшествия?"
                : caseData.firstQuestion;

            StartCoroutine(ui.TypeInvestigatorLine(currentQuestion));
        }

        public void SubmitAnswer(string answer)
        {
            if (!CanSubmitAnswer)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(answer))
            {
                return;
            }

            var trimmedAnswer = answer.Trim();
            if (IsEvidenceCommand(trimmedAnswer))
            {
                ui.AppendPlayer(trimmedAnswer);
                ui.AppendSystem("Доступ к уликам открыт. Осмотрите дверь справа.");
                audioController.PlayTerminalBeep();
                ui.ShowEvidenceRoom();
                return;
            }

            StartCoroutine(HandleAnswerRoutine(trimmedAnswer));
        }

        public void RestartGame()
        {
            ShowMainMenu();
        }

        public void ReturnToMainMenu()
        {
            audioController.PlaySubmit();
            ShowMainMenu();
        }

        public void OpenEvidenceFromButton()
        {
            if (State != GameFlowState.Interrogation)
            {
                return;
            }

            if (busy)
            {
                audioController.PlayTerminalBeep();
                ui.AppendSystem("Осмотр улик недоступен: дождитесь ответа следователя.");
                return;
            }

            audioController.PlayTerminalBeep();
            ui.AppendSystem("Доступ к уликам открыт. Осмотрите дверь справа.");
            ui.ShowEvidenceRoom();
        }

        private void Update()
        {
            if ((State == GameFlowState.Briefing || State == GameFlowState.Interrogation || State == GameFlowState.FinalReport) &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToMainMenu();
                return;
            }

            if (State == GameFlowState.FinalReport && Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
        }

        private IEnumerator HandleAnswerRoutine(string answer)
        {
            busy = true;
            ui.SetInputLocked(true);
            ui.AppendPlayer(answer);

            var nextTurn = currentTurn + 1;
            var aggressiveAnswer = IsAggressiveCommand(answer);
            var analysis = analysisSystem.Analyze(answer, memorySystem, caseData, currentQuestion);
            var delta = suspicionSystem.Apply(analysis);
            if (aggressiveAnswer)
            {
                suspicionSystem.AddSuspicion(aggressionSuspicionDelta);
            }

            currentTurn = nextTurn;
            memorySystem.Remember(currentTurn, currentQuestion, answer, analysis, suspicionSystem.Suspicion);
            ui.SetSuspicion(suspicionSystem.Suspicion);
            ui.AppendAnalysis(analysis, delta);
            if (aggressiveAnswer)
            {
                ui.AppendSystem($"Агрессия в ответе: +{aggressionSuspicionDelta} подозрения.");
            }

            var mood = suspicionSystem.ResolveMood(analysis);
            sceneAtmosphere.SetMood(mood, analysis.HasBadAnswer);
            if (aggressiveAnswer)
            {
                RegisterTableSlam(currentTurn);
                sceneAtmosphere.TriggerTableSlam(tableSlamReturnDelay);
                audioController.PlayTableSlam();
                ui.ShakeTerminal();
            }
            else if (ShouldTriggerTableSlam(currentTurn))
            {
                RegisterTableSlam(currentTurn);
                sceneAtmosphere.TriggerTableSlam(tableSlamReturnDelay);
                audioController.PlayTableSlam();
                ui.ShakeTerminal();
            }
            else if (mood == InterrogatorMood.Angry && analysis.HasBadAnswer)
            {
                ui.ShakeTerminal();
                audioController.PlayAngerHit();
            }

            if (currentTurn >= maxTurns)
            {
                yield return new WaitForSeconds(0.45f);
                ShowFinalReport();
                yield break;
            }

            yield return new WaitForSeconds(Random.Range(0.4f, 0.8f));

            var context = new DialogueContext
            {
                caseData = caseData,
                history = memorySystem.Records,
                lastAnalysis = analysis,
                lastAnswer = answer,
                lastQuestion = currentQuestion,
                turn = currentTurn,
                maxTurns = maxTurns,
                suspicion = suspicionSystem.Suspicion,
                memorySummary = memorySystem.BuildSummary()
            };

            ui.SetStatus("WAIT");
            Task<AIResponse> task = dialogueManager.GetNextQuestionAsync(context, cancellation.Token);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            AIResponse response;
            if (task.IsCanceled)
            {
                busy = false;
                yield break;
            }

            if (task.IsFaulted)
            {
                var message = task.Exception?.GetBaseException().Message ?? "неизвестная ошибка";
                response = AIResponse.FromText("Продолжайте. Где вы были?", true, "AI ошибка: " + message + ". Включен MockAIClient.");
            }
            else
            {
                response = task.Result;
            }

            if (!string.IsNullOrWhiteSpace(response.errorMessage))
            {
                ui.AppendSystem(response.errorMessage);
            }

            currentQuestion = response.text;
            ui.SetStatus(response.usedMock ? "MOCK" : "AI");
            audioController.PlayTerminalBeep();
            yield return ui.TypeInvestigatorLine(currentQuestion);
            ui.SetStatus("REC");
            busy = false;
            ui.SetInputLocked(false);
        }

        private void ShowMainMenu()
        {
            StopAllCoroutines();
            cancellation?.Cancel();
            busy = false;
            State = GameFlowState.MainMenu;
            sceneAtmosphere.ForceMood(InterrogatorMood.Calm);
            ui.ShowMainMenu();
        }

        private void ShowBriefing()
        {
            StopAllCoroutines();
            cancellation?.Cancel();
            busy = false;
            State = GameFlowState.Briefing;
            sceneAtmosphere.ForceMood(InterrogatorMood.Calm);
            ui.ShowBriefing(caseData);
        }

        private void ShowFinalReport()
        {
            busy = false;
            State = GameFlowState.FinalReport;
            ui.SetInputLocked(true);
            ui.SetStatus("END");
            var report = ReportBuilder.BuildFinalReport(caseData, memorySystem.Records, suspicionSystem.Suspicion);
            audioController.PlayFinalSting();
            ui.ShowFinalReport(report);
        }

        private CaseData LoadCase()
        {
            var asset = Resources.Load<TextAsset>("Cases/case_01");
            if (asset == null)
            {
                Debug.LogWarning("Case file not found. Fallback case loaded.");
                return CaseData.CreateFallback();
            }

            try
            {
                var loaded = JsonUtility.FromJson<CaseData>(asset.text);
                return loaded ?? CaseData.CreateFallback();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Case parse failed: " + exception.Message);
                return CaseData.CreateFallback();
            }
        }

        public bool ShouldTriggerTableSlam(int answerIndex)
        {
            return ShouldTriggerTableSlam(answerIndex, Random.value);
        }

        public bool ShouldTriggerTableSlam(int answerIndex, float randomRoll)
        {
            if (answerIndex < firstGuaranteedTableSlamAnswer)
            {
                return false;
            }

            if (lastTableSlamAnswer < 0)
            {
                return answerIndex >= firstGuaranteedTableSlamAnswer;
            }

            var minInterval = Mathf.Max(1, minTableSlamInterval);
            var maxInterval = Mathf.Max(minInterval, maxTableSlamInterval);
            var intervalSinceLastSlam = answerIndex - lastTableSlamAnswer;

            if (intervalSinceLastSlam < minInterval)
            {
                return false;
            }

            if (intervalSinceLastSlam >= maxInterval)
            {
                return true;
            }

            return randomRoll <= Mathf.Clamp01(tableSlamChance);
        }

        public void RegisterTableSlam(int answerIndex)
        {
            lastTableSlamAnswer = answerIndex;
        }

        private static bool IsEvidenceCommand(string answer)
        {
            var normalized = NormalizeEvidenceCommand(answer);
            return normalized == "улики" ||
                   normalized == "улики дай" ||
                   normalized == "дай улики" ||
                   normalized == "хочу улики" ||
                   normalized == "осмотреть улики" ||
                   normalized == "посмотреть улики" ||
                   normalized == "показать улики" ||
                   normalized == "покажи улики" ||
                   normalized == "хочу глянуть улики" ||
                   normalized.Contains("хочу посмотреть улики") ||
                   normalized.Contains("хочу осмотреть улики") ||
                   normalized.Contains("хочу глянуть улики");
        }

        private static bool IsAggressiveCommand(string answer)
        {
            var normalized = NormalizeEvidenceCommand(answer);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return ContainsNormalizedPhrase(normalized, "сука открой дверь") ||
                   ContainsNormalizedPhrase(normalized, "тварина дай глянуть на улики") ||
                   ContainsNormalizedPhrase(normalized, "пидарас") ||
                   ContainsNormalizedPhrase(normalized, "сучара") ||
                   ContainsNormalizedPhrase(normalized, "сука") ||
                   ContainsNormalizedPhrase(normalized, "вафля") ||
                   ContainsNormalizedPhrase(normalized, "я тебя выебу") ||
                   ContainsNormalizedPhrase(normalized, "уебок") ||
                   ContainsNormalizedPhrase(normalized, "черт");
        }

        private static bool ContainsNormalizedPhrase(string normalized, string phrase)
        {
            var normalizedPhrase = NormalizeEvidenceCommand(phrase);
            return normalized == normalizedPhrase ||
                   normalized.StartsWith(normalizedPhrase + " ") ||
                   normalized.EndsWith(" " + normalizedPhrase) ||
                   normalized.Contains(" " + normalizedPhrase + " ");
        }

        private static string NormalizeEvidenceCommand(string text)
        {
            var source = (text ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');
            var builder = new StringBuilder(source.Length);
            var lastWasSpace = false;

            foreach (var character in source)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        private T GetOrAdd<T>() where T : Component
        {
            var component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
