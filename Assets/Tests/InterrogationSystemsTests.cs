using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AIInterrogation.Tests
{
    public class InterrogationSystemsTests
    {
        private GameObject testObject;
        private AnalysisSystem analysisSystem;
        private MemorySystem memorySystem;
        private SuspicionSystem suspicionSystem;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject("Interrogation Systems Test");
            analysisSystem = testObject.AddComponent<AnalysisSystem>();
            memorySystem = testObject.AddComponent<MemorySystem>();
            suspicionSystem = testObject.AddComponent<SuspicionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testObject);
        }

        [Test]
        public void CaseJsonLoadsCoreBriefingFields()
        {
            var json = Resources.Load<TextAsset>("Cases/case_01");
            Assert.NotNull(json);

            var data = JsonUtility.FromJson<CaseData>(json.text);
            Assert.AreEqual("Ночной техник станции метро", data.role);
            Assert.IsFalse(string.IsNullOrWhiteSpace(data.truth));
            Assert.IsNotEmpty(data.risks);
            Assert.IsNotEmpty(data.firstQuestion);
        }

        [Test]
        public void AudioResourcesArePresent()
        {
            Assert.NotNull(Resources.Load<AudioClip>("Audio/room_ambience"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/lamp_buzz"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/type_click"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/input_submit"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/folder_open"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/anger_hit"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/table_slam"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/final_sting"));
            Assert.NotNull(Resources.Load<AudioClip>("Audio/terminal_beep"));
        }

        [Test]
        public void TableSlamUsesGuaranteedFirstHitThenRandomIntervals()
        {
            var flowObject = new GameObject("Flow Slam Rule Test");
            try
            {
                var flow = flowObject.AddComponent<GameFlowController>();

                Assert.IsFalse(flow.ShouldTriggerTableSlam(1, 0f));
                Assert.IsFalse(flow.ShouldTriggerTableSlam(2, 0f));
                Assert.IsFalse(flow.ShouldTriggerTableSlam(3, 0f));

                Assert.IsTrue(flow.ShouldTriggerTableSlam(4, 1f));
                flow.RegisterTableSlam(4);

                Assert.IsFalse(flow.ShouldTriggerTableSlam(5, 0f));
                Assert.IsFalse(flow.ShouldTriggerTableSlam(6, 0.99f));
                Assert.IsTrue(flow.ShouldTriggerTableSlam(6, 0f));
                Assert.IsTrue(flow.ShouldTriggerTableSlam(11, 0.99f));
            }
            finally
            {
                Object.DestroyImmediate(flowObject);
            }
        }

        [Test]
        public void MenuResourcesArePresent()
        {
            Assert.NotNull(Resources.Load<Texture2D>("Art/menu_closed"));
            Assert.NotNull(Resources.Load<Texture2D>("Art/menu_open"));
        }

        [TestCase("Я был там в 23:40.", "23:40")]
        [TestCase("примерно в 7 часов", "07:00")]
        [TestCase("ночью я был один", "ночь")]
        public void NormalizesTime(string answer, string expected)
        {
            Assert.AreEqual(expected, analysisSystem.NormalizeTime(answer));
        }

        [Test]
        public void DetectsContradictingTimeAndLocation()
        {
            var caseData = CaseData.CreateFallback();
            var first = analysisSystem.Analyze("Я был в электрощитовой B-12 в 23:40.", memorySystem, caseData, "Где вы были?");
            memorySystem.Remember(1, "Где вы были?", "Я был в электрощитовой B-12 в 23:40.", first, 0);

            var second = analysisSystem.Analyze("Нет, в 23:55 я был в тоннеле.", memorySystem, caseData, "Повторите.");

            Assert.IsTrue(second.contradiction);
            Assert.AreEqual("23:55", second.normalizedTime);
        }

        [Test]
        public void DetectsAvoidance()
        {
            var result = analysisSystem.Analyze("Не помню, какая разница.", memorySystem, CaseData.CreateFallback(), "Где вы были?");

            Assert.IsTrue(result.avoidance);
        }

        [Test]
        public void AddsSuspicionByRule()
        {
            var delta = suspicionSystem.Apply(new AnalysisResult
            {
                contradiction = true,
                avoidance = true,
                extraDetail = true
            });

            Assert.AreEqual(25, delta.totalDelta);
            Assert.AreEqual(25, suspicionSystem.Suspicion);
        }

        [TestCase(0, "Не виновен")]
        [TestCase(20, "Не виновен")]
        [TestCase(21, "Недостаточно улик")]
        [TestCase(40, "Недостаточно улик")]
        [TestCase(41, "Подозрителен")]
        [TestCase(70, "Подозрителен")]
        [TestCase(71, "Виновен")]
        public void MapsVerdicts(int suspicion, string expected)
        {
            Assert.AreEqual(expected, VerdictRules.MapVerdict(suspicion));
        }

        [Test]
        public async Task MockClientCanDriveTwelveTurns()
        {
            var mock = new MockAIClient();
            var caseData = CaseData.CreateFallback();
            var question = caseData.firstQuestion;

            for (var turn = 1; turn <= 12; turn++)
            {
                var answer = turn % 3 == 0
                    ? "Не помню, возможно в тоннеле около 23:55."
                    : "Я был в электрощитовой B-12 в 23:40.";

                var analysis = analysisSystem.Analyze(answer, memorySystem, caseData, question);
                var delta = suspicionSystem.Apply(analysis);
                memorySystem.Remember(turn, question, answer, analysis, suspicionSystem.Suspicion);

                var response = await mock.GetNextQuestionAsync(new DialogueContext
                {
                    caseData = caseData,
                    history = memorySystem.Records,
                    lastAnalysis = analysis,
                    lastAnswer = answer,
                    lastQuestion = question,
                    turn = turn,
                    maxTurns = 12,
                    suspicion = suspicionSystem.Suspicion,
                    memorySummary = memorySystem.BuildSummary()
                }, CancellationToken.None);

                Assert.IsTrue(response.usedMock);
                Assert.IsFalse(string.IsNullOrWhiteSpace(response.text));
                Assert.GreaterOrEqual(delta.totalDelta, 0);
                question = response.text;
            }

            Assert.AreEqual(12, memorySystem.Records.Count);
        }

        [Test]
        public void FlowTransitionsBriefingInterrogationRestart()
        {
            var flowObject = new GameObject("Flow Test");
            try
            {
                var flow = flowObject.AddComponent<GameFlowController>();
                flow.InitializeRuntime();

                Assert.AreEqual(GameFlowState.MainMenu, flow.State);
                flow.NewCaseFromMenu();
                Assert.AreEqual(GameFlowState.Briefing, flow.State);
                flow.StartInterrogation();
                Assert.AreEqual(GameFlowState.Interrogation, flow.State);
                Assert.IsTrue(flow.CanSubmitAnswer);

                flow.RestartGame();
                Assert.AreEqual(GameFlowState.MainMenu, flow.State);
            }
            finally
            {
                Object.DestroyImmediate(flowObject);
                DestroyGeneratedRuntimeObject("Interrogation UI Canvas");
                DestroyGeneratedRuntimeObject("CRT Atmosphere Canvas");
                DestroyGeneratedRuntimeObject("Interrogator Fullscreen Sprite");
                DestroyGeneratedRuntimeObject("Main Camera");
                DestroyGeneratedRuntimeObject("EventSystem");
                DestroyGeneratedRuntimeObject("Audio Ambience");
                DestroyGeneratedRuntimeObject("Audio Lamp Buzz");
                DestroyGeneratedRuntimeObject("Audio SFX");
                DestroyGeneratedRuntimeObject("Audio UI");
            }
        }

        private static void DestroyGeneratedRuntimeObject(string objectName)
        {
            var generated = GameObject.Find(objectName);
            if (generated != null)
            {
                Object.DestroyImmediate(generated);
            }
        }
    }
}
