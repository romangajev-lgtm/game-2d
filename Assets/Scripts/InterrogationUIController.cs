using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AIInterrogation
{
    public class InterrogationUIController : MonoBehaviour
    {
        private enum EvidenceRoomState
        {
            FirstRoom,
            SecondRoom,
            BriefcaseOpen
        }

        private const string ResolutionPrefKey = "AIInterrogation.ResolutionIndex";
        private const string FullscreenPrefKey = "AIInterrogation.Fullscreen";
        private const float MenuVideoPrepareTimeoutSeconds = 2.5f;
        private static readonly bool MainMenuVideoEnabled = true;

        private readonly List<string> logLines = new List<string>();
        private readonly Vector2Int[] supportedResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080)
        };

        private GameFlowController flow;
        private AudioController audioController;
        private Font monoFont;
        private Canvas canvas;
        private GameObject mainMenuPanel;
        private GameObject briefingPanel;
        private GameObject terminalPanel;
        private GameObject finalPanel;
        private GameObject evidencePanel;
        private GameObject quickMenuButton;
        private GameObject evidenceAccessButton;
        private Image menuClosedImage;
        private Image menuOpenImage;
        private RawImage menuVideoImage;
        private VideoPlayer menuVideoPlayer;
        private RenderTexture menuVideoTexture;
        private CanvasGroup menuClosedTitleGroup;
        private CanvasGroup menuItemsGroup;
        private CanvasGroup menuSettingsGroup;
        private RectTransform menuItemsRect;
        private RectTransform menuSettingsRect;
        private Vector2 menuItemsBasePosition;
        private Coroutine menuOpenRoutine;
        private Text menuMessageText;
        private Text settingsResolutionText;
        private Text settingsFullscreenText;
        private Text briefingText;
        private Text logText;
        private Text statusText;
        private Text suspicionText;
        private Text waitingText;
        private Text finalText;
        private Text evidenceHintText;
        private InputField inputField;
        private Button sendButton;
        private Button evidenceBackButton;
        private Button evidenceDoorButton;
        private Button evidenceBriefcaseButton;
        private ScrollRect scrollRect;
        private RectTransform terminalRect;
        private float terminalShakeTime;
        private int lastSubmitFrame = -1;
        private Vector2 terminalBasePosition;
        private Image evidenceRoomImage;
        private Sprite evidenceRoomOneSprite;
        private Sprite evidenceRoomOneDoorOpenSprite;
        private Sprite evidenceRoomTwoSprite;
        private Sprite evidenceRoomTwoBriefcaseOpenSprite;
        private EvidenceRoomState evidenceRoomState;
        private bool menuIntroPlayed;
        private bool selectedFullscreen;
        private int selectedResolutionIndex;

        public bool InputLocked { get; private set; }

        public void Initialize(GameFlowController flowController, AudioController audio)
        {
            flow = flowController;
            audioController = audio;
            monoFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            CreateEventSystem();
            CreateCanvas();
            CreateMainMenuPanel();
            CreateBriefingPanel();
            CreateTerminalPanel();
            CreateEvidencePanel();
            CreateFinalPanel();
            CreateQuickMenuButton();
            CreateEvidenceAccessButton();
            LoadDisplaySettings();
            SetWaiting(false);
        }

        public void ShowMainMenu()
        {
            mainMenuPanel.SetActive(true);
            briefingPanel.SetActive(false);
            terminalPanel.SetActive(false);
            finalPanel.SetActive(false);
            evidencePanel.SetActive(false);
            quickMenuButton.SetActive(false);
            evidenceAccessButton.SetActive(false);
            SetMenuMessage(string.Empty);
            if (menuOpenRoutine != null)
            {
                StopCoroutine(menuOpenRoutine);
            }

            if (menuIntroPlayed || !MainMenuVideoEnabled)
            {
                ShowMenuOpenState();
                menuIntroPlayed = true;
                return;
            }

            menuOpenRoutine = StartCoroutine(AnimateMenuOpen());
        }

        public void SetMenuMessage(string message)
        {
            if (menuMessageText != null)
            {
                menuMessageText.text = message ?? string.Empty;
            }
        }

        public void ShowBriefing(CaseData caseData)
        {
            menuIntroPlayed = true;
            mainMenuPanel.SetActive(false);
            briefingPanel.SetActive(true);
            terminalPanel.SetActive(false);
            finalPanel.SetActive(false);
            evidencePanel.SetActive(false);
            quickMenuButton.SetActive(true);
            evidenceAccessButton.SetActive(false);

            var risks = caseData.risks == null || caseData.risks.Length == 0
                ? "нет"
                : "- " + string.Join("\n- ", caseData.risks);

            briefingText.text =
                $"{caseData.title}\n\n" +
                $"РОЛЬ: {caseData.role}\n\n" +
                $"СИТУАЦИЯ:\n{caseData.situation}\n\n" +
                $"ПРАВДА:\n{caseData.truth}\n\n" +
                $"РИСКИ:\n{risks}\n\n" +
                $"ЦЕЛЬ:\n{caseData.goal}";
        }

        public void ShowInterrogation()
        {
            mainMenuPanel.SetActive(false);
            briefingPanel.SetActive(false);
            finalPanel.SetActive(false);
            evidencePanel.SetActive(false);
            terminalPanel.SetActive(true);
            quickMenuButton.SetActive(true);
            evidenceAccessButton.SetActive(true);
            logLines.Clear();
            logText.text = string.Empty;
            SetInputLocked(false);
            FocusInput();
        }

        public void ShowFinalReport(string report)
        {
            mainMenuPanel.SetActive(false);
            briefingPanel.SetActive(false);
            terminalPanel.SetActive(false);
            finalPanel.SetActive(true);
            evidencePanel.SetActive(false);
            quickMenuButton.SetActive(true);
            evidenceAccessButton.SetActive(false);
            finalText.text = report;
        }

        public void ShowEvidenceRoom()
        {
            mainMenuPanel.SetActive(false);
            briefingPanel.SetActive(false);
            terminalPanel.SetActive(false);
            finalPanel.SetActive(false);
            quickMenuButton.SetActive(false);
            evidenceAccessButton.SetActive(false);
            evidencePanel.SetActive(true);
            SetInputLocked(true);
            ShowEvidenceFirstRoom();
        }

        public void HideEvidenceRoomToInterrogation()
        {
            evidencePanel.SetActive(false);
            terminalPanel.SetActive(true);
            quickMenuButton.SetActive(true);
            evidenceAccessButton.SetActive(true);
            SetInputLocked(false);
            FocusInput();
        }

        public void SetSuspicion(int suspicion)
        {
            suspicionText.text = $"SUSPICION {suspicion:000}/100";
        }

        public void SetStatus(string status)
        {
            statusText.text = status;
        }

        public void SetInputLocked(bool locked)
        {
            InputLocked = locked;
            inputField.interactable = !locked;
            inputField.text = string.Empty;
            if (sendButton != null)
            {
                sendButton.interactable = !locked;
            }

            SetWaiting(locked);

            if (!locked)
            {
                FocusInput();
            }
        }

        public void ShakeTerminal()
        {
            terminalShakeTime = 0.22f;
        }

        public void AppendPlayer(string answer)
        {
            AddLogLine("> " + answer, new Color(0.74f, 0.96f, 0.62f, 1f));
        }

        public void AppendSystem(string text)
        {
            AddLogLine("[sys] " + text, new Color(0.95f, 0.68f, 0.34f, 1f));
        }

        public void AppendAnalysis(AnalysisResult result, SuspicionDelta delta)
        {
            if (result == null || delta == null || delta.totalDelta <= 0)
            {
                return;
            }

            AddLogLine($"[analysis] {result.BuildShortReason()} {delta}", new Color(0.95f, 0.68f, 0.34f, 1f));
        }

        public IEnumerator TypeInvestigatorLine(string line)
        {
            var prefix = "<color=#86ff76>СЛЕДОВАТЕЛЬ:</color> ";
            var cleanLine = (line ?? string.Empty).Trim();
            var visible = new StringBuilder();
            for (var i = 0; i < cleanLine.Length; i++)
            {
                visible.Append(cleanLine[i]);
                RenderLogWithTemporaryLine(prefix + Escape(visible.ToString()));
                if (!char.IsWhiteSpace(cleanLine[i]))
                {
                    audioController?.PlayTypeClick();
                }

                yield return new WaitForSeconds(0.018f);
            }

            AddRawLogLine(prefix + Escape(cleanLine));
        }

        private void Update()
        {
            if (terminalPanel != null && terminalPanel.activeInHierarchy && !InputLocked)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    TrySubmitCurrentInput();
                }

                if (!inputField.isFocused)
                {
                    FocusInput();
                }
            }

            UpdateTerminalShake();

            if (mainMenuPanel != null &&
                mainMenuPanel.activeInHierarchy &&
                menuSettingsGroup != null &&
                menuSettingsGroup.interactable &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                audioController?.PlaySubmit();
                ShowMainMenuItems();
            }
        }

        private IEnumerator AnimateMenuOpen()
        {
            if (menuVideoPlayer != null && menuVideoPlayer.clip != null)
            {
                yield return AnimateMenuVideoOpen();
                yield break;
            }

            yield return AnimateStaticMenuOpen();
        }

        private IEnumerator AnimateStaticMenuOpen()
        {
            if (menuVideoImage != null)
            {
                menuVideoImage.gameObject.SetActive(false);
            }

            menuClosedImage.gameObject.SetActive(true);
            menuOpenImage.gameObject.SetActive(true);
            menuClosedImage.color = Color.white;
            menuOpenImage.color = new Color(1f, 1f, 1f, 0f);
            menuClosedTitleGroup.alpha = 1f;
            menuItemsGroup.alpha = 0f;
            menuItemsGroup.interactable = false;
            menuItemsGroup.blocksRaycasts = false;
            menuItemsRect.anchoredPosition = menuItemsBasePosition + new Vector2(24f, -8f);

            yield return new WaitForSeconds(0.35f);
            audioController?.PlayFolderOpen();

            const float duration = 1.15f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                menuClosedImage.color = new Color(1f, 1f, 1f, 1f - t);
                menuOpenImage.color = new Color(1f, 1f, 1f, t);
                menuClosedTitleGroup.alpha = 1f - Mathf.Clamp01(t * 1.4f);
                menuItemsGroup.alpha = Mathf.Clamp01((t - 0.35f) / 0.65f);
                menuItemsRect.anchoredPosition = Vector2.Lerp(menuItemsBasePosition + new Vector2(24f, -8f), menuItemsBasePosition, t);
                yield return null;
            }

            menuClosedImage.color = new Color(1f, 1f, 1f, 0f);
            menuOpenImage.color = Color.white;
            menuClosedTitleGroup.alpha = 0f;
            menuItemsGroup.alpha = 1f;
            menuItemsGroup.interactable = true;
            menuItemsGroup.blocksRaycasts = true;
            menuItemsRect.anchoredPosition = menuItemsBasePosition;
            menuIntroPlayed = true;
            menuOpenRoutine = null;
        }

        private IEnumerator AnimateMenuVideoOpen()
        {
            menuClosedImage.gameObject.SetActive(false);
            menuOpenImage.gameObject.SetActive(false);
            menuVideoImage.gameObject.SetActive(true);
            menuVideoImage.color = Color.white;
            menuClosedTitleGroup.alpha = 0f;
            menuItemsGroup.alpha = 0f;
            menuItemsGroup.interactable = false;
            menuItemsGroup.blocksRaycasts = false;
            menuItemsRect.anchoredPosition = menuItemsBasePosition + new Vector2(24f, -8f);

            menuVideoPlayer.Stop();
            menuVideoPlayer.time = 0d;
            menuVideoPlayer.frame = 0;
            menuVideoPlayer.Prepare();

            var prepareElapsed = 0f;
            while (!menuVideoPlayer.isPrepared)
            {
                prepareElapsed += Time.unscaledDeltaTime;
                if (prepareElapsed >= MenuVideoPrepareTimeoutSeconds)
                {
                    Debug.LogWarning("Main menu video prepare timed out. Falling back to static menu images.");
                    menuVideoPlayer.Stop();
                    yield return AnimateStaticMenuOpen();
                    yield break;
                }

                yield return null;
            }

            audioController?.PlayFolderOpen();
            menuVideoPlayer.Play();

            var menuRevealStarted = false;
            while (menuVideoPlayer.isPlaying)
            {
                var progress = menuVideoPlayer.length > 0.01d
                    ? Mathf.Clamp01((float)(menuVideoPlayer.time / menuVideoPlayer.length))
                    : 0f;

                if (progress >= 0.48f)
                {
                    menuRevealStarted = true;
                }

                if (menuRevealStarted)
                {
                    var t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.88f, progress));
                    menuItemsGroup.alpha = t;
                    menuItemsRect.anchoredPosition = Vector2.Lerp(menuItemsBasePosition + new Vector2(24f, -8f), menuItemsBasePosition, t);
                }

                yield return null;
            }

            menuVideoPlayer.Pause();
            if (menuVideoPlayer.frameCount > 1)
            {
                menuVideoPlayer.frame = (long)menuVideoPlayer.frameCount - 1;
            }

            menuItemsGroup.alpha = 1f;
            menuItemsGroup.interactable = true;
            menuItemsGroup.blocksRaycasts = true;
            menuItemsRect.anchoredPosition = menuItemsBasePosition;
            menuIntroPlayed = true;
            menuOpenRoutine = null;
        }

        private void ShowMenuOpenState()
        {
            if (menuVideoPlayer != null)
            {
                menuVideoPlayer.Pause();
            }

            if (menuVideoImage != null)
            {
                menuVideoImage.gameObject.SetActive(false);
            }

            if (menuClosedImage != null)
            {
                menuClosedImage.gameObject.SetActive(true);
                menuClosedImage.color = new Color(1f, 1f, 1f, 0f);
            }

            if (menuOpenImage != null)
            {
                menuOpenImage.gameObject.SetActive(true);
                menuOpenImage.color = Color.white;
            }

            menuClosedTitleGroup.alpha = 0f;
            menuItemsGroup.alpha = 1f;
            menuItemsGroup.interactable = true;
            menuItemsGroup.blocksRaycasts = true;
            menuItemsRect.anchoredPosition = menuItemsBasePosition;
            ShowMainMenuItems();
            menuOpenRoutine = null;
        }

        private void TrySubmitCurrentInput()
        {
            if (lastSubmitFrame == Time.frameCount)
            {
                return;
            }

            lastSubmitFrame = Time.frameCount;
            SubmitCurrentInput();
        }

        private void SubmitCurrentInput()
        {
            if (InputLocked || inputField == null)
            {
                return;
            }

            var answer = inputField.text.Trim();
            if (string.IsNullOrWhiteSpace(answer))
            {
                FocusInput();
                return;
            }

            inputField.text = string.Empty;
            audioController?.PlaySubmit();
            flow.SubmitAnswer(answer);
        }

        private void AddLogLine(string text, Color color)
        {
            var hex = ColorUtility.ToHtmlStringRGB(color);
            AddRawLogLine($"<color=#{hex}>{Escape(text)}</color>");
        }

        private void AddRawLogLine(string richText)
        {
            logLines.Add(richText);
            TrimLog();
            logText.text = string.Join("\n\n", logLines);
            ScrollToBottom();
        }

        private void RenderLogWithTemporaryLine(string line)
        {
            TrimLog();
            if (logLines.Count == 0)
            {
                logText.text = line;
            }
            else
            {
                logText.text = string.Join("\n\n", logLines) + "\n\n" + line;
            }

            ScrollToBottom();
        }

        private void TrimLog()
        {
            while (logLines.Count > 18)
            {
                logLines.RemoveAt(0);
            }
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void SetWaiting(bool waiting)
        {
            waitingText.gameObject.SetActive(waiting);
            waitingText.text = waiting ? "ожидание..." : string.Empty;
        }

        private void FocusInput()
        {
            if (inputField == null || !inputField.interactable)
            {
                return;
            }

            inputField.ActivateInputField();
            inputField.Select();
        }

        private void UpdateTerminalShake()
        {
            if (terminalRect == null)
            {
                return;
            }

            if (terminalShakeTime <= 0f)
            {
                terminalRect.anchoredPosition = terminalBasePosition;
                return;
            }

            terminalShakeTime -= Time.deltaTime;
            var amount = Mathf.Lerp(0f, 5f, terminalShakeTime / 0.22f);
            terminalRect.anchoredPosition = terminalBasePosition + new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount));
        }

        private void CreateEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void CreateCanvas()
        {
            var canvasObject = new GameObject("Interrogation UI Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void CreateMainMenuPanel()
        {
            mainMenuPanel = CreateFullscreenPanel("Main Menu", Color.black);

            menuClosedImage = CreateFullscreenImage("Menu Closed Frame", mainMenuPanel.transform, "Art/menu_closed");
            menuOpenImage = CreateFullscreenImage("Menu Open Frame", mainMenuPanel.transform, "Art/menu_open");
            if (MainMenuVideoEnabled)
            {
                menuVideoImage = CreateFullscreenVideo("Menu Folder Video", mainMenuPanel.transform, "Video/start");
            }

            CreateClosedFolderTitle();

            var shade = new GameObject("Menu Dark Edge");
            shade.transform.SetParent(mainMenuPanel.transform, false);
            var shadeRect = shade.AddComponent<RectTransform>();
            shadeRect.StretchToParent();
            var shadeImage = shade.AddComponent<Image>();
            shadeImage.sprite = TextureFactory.CreateSolidSprite(Color.black);
            shadeImage.color = new Color(0f, 0f, 0f, 0.10f);
            RuntimeMaterialFactory.ApplyTo(shadeImage);
            shadeImage.raycastTarget = false;

            var groupObject = new GameObject("Menu Items");
            groupObject.transform.SetParent(mainMenuPanel.transform, false);
            menuItemsRect = groupObject.AddComponent<RectTransform>();
            menuItemsRect.anchorMin = new Vector2(0.54f, 0.27f);
            menuItemsRect.anchorMax = new Vector2(0.80f, 0.71f);
            menuItemsRect.offsetMin = Vector2.zero;
            menuItemsRect.offsetMax = Vector2.zero;
            menuItemsBasePosition = menuItemsRect.anchoredPosition;
            menuItemsGroup = groupObject.AddComponent<CanvasGroup>();

            var title = CreateText("Menu Title", groupObject.transform, 26, new Color(0.10f, 0.075f, 0.042f, 0.96f), TextAnchor.UpperLeft);
            title.text = "МАТЕРИАЛЫ ДЕЛА";
            title.text = "AI INTERROGATION";
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.02f, 0.84f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var subtitle = CreateText("Menu Subtitle", groupObject.transform, 14, new Color(0.22f, 0.11f, 0.06f, 0.86f), TextAnchor.UpperLeft);
            subtitle.text = "АРХИВ / ДОПРОСНАЯ 23-40";
            var subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.03f, 0.75f);
            subtitleRect.anchorMax = new Vector2(1f, 0.84f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            var newCaseButton = CreateButton("НОВОЕ ДЕЛО", groupObject.transform, new Vector2(0.03f, 0.56f), new Vector2(0.82f, 0.70f));
            newCaseButton.onClick.AddListener(() => flow.NewCaseFromMenu());
            RestyleCaseMenuButton(newCaseButton, "НОВОЕ ДЕЛО", new Vector2(0.02f, 0.56f), new Vector2(0.88f, 0.68f));

            var loadButton = CreateButton("ЗАГРУЗКА", groupObject.transform, new Vector2(0.03f, 0.38f), new Vector2(0.82f, 0.52f));
            loadButton.onClick.AddListener(() => flow.LoadGameFromMenu());
            RestyleCaseMenuButton(loadButton, "ЗАГРУЗКА", new Vector2(0.02f, 0.40f), new Vector2(0.88f, 0.52f));

            var exitButton = CreateButton("ВЫХОД", groupObject.transform, new Vector2(0.03f, 0.20f), new Vector2(0.82f, 0.34f));
            exitButton.onClick.AddListener(() => flow.ExitFromMenu());
            RestyleCaseMenuButton(exitButton, "ВЫХОД", new Vector2(0.02f, 0.24f), new Vector2(0.88f, 0.36f));

            var settingsButton = CreateButton("НАСТРОЙКИ", groupObject.transform, new Vector2(0.50f, 0.20f), new Vector2(0.98f, 0.34f));
            settingsButton.onClick.AddListener(OpenSettingsMenu);
            RestyleCaseMenuButton(settingsButton, "НАСТРОЙКИ", new Vector2(0.50f, 0.24f), new Vector2(0.98f, 0.36f));
            var exitRect = exitButton.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(0.02f, 0.24f);
            exitRect.anchorMax = new Vector2(0.46f, 0.36f);

            menuMessageText = CreateText("Menu Message", groupObject.transform, 14, new Color(0.16f, 0.05f, 0.025f, 1f), TextAnchor.UpperLeft);
            var messageRect = menuMessageText.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0.03f, 0.02f);
            messageRect.anchorMax = new Vector2(0.98f, 0.16f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;

            CreateSettingsMenuPanel();
        }

        private void CreateSettingsMenuPanel()
        {
            var settingsObject = new GameObject("Menu Settings");
            settingsObject.transform.SetParent(mainMenuPanel.transform, false);
            menuSettingsRect = settingsObject.AddComponent<RectTransform>();
            menuSettingsRect.anchorMin = new Vector2(0.54f, 0.27f);
            menuSettingsRect.anchorMax = new Vector2(0.80f, 0.71f);
            menuSettingsRect.offsetMin = Vector2.zero;
            menuSettingsRect.offsetMax = Vector2.zero;
            menuSettingsGroup = settingsObject.AddComponent<CanvasGroup>();
            menuSettingsGroup.alpha = 0f;
            menuSettingsGroup.interactable = false;
            menuSettingsGroup.blocksRaycasts = false;

            var title = CreateText("Settings Title", settingsObject.transform, 25, new Color(0.10f, 0.075f, 0.042f, 0.96f), TextAnchor.UpperLeft);
            title.text = "НАСТРОЙКИ";
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.02f, 0.84f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var graphicsLabel = CreateText("Graphics Label", settingsObject.transform, 16, new Color(0.22f, 0.11f, 0.06f, 0.86f), TextAnchor.UpperLeft);
            graphicsLabel.text = "ГРАФИКА";
            var graphicsRect = graphicsLabel.GetComponent<RectTransform>();
            graphicsRect.anchorMin = new Vector2(0.03f, 0.74f);
            graphicsRect.anchorMax = new Vector2(1f, 0.84f);
            graphicsRect.offsetMin = Vector2.zero;
            graphicsRect.offsetMax = Vector2.zero;

            settingsResolutionText = CreateText("Resolution Value", settingsObject.transform, 15, new Color(0.12f, 0.06f, 0.03f, 0.95f), TextAnchor.UpperLeft);
            var resolutionRect = settingsResolutionText.GetComponent<RectTransform>();
            resolutionRect.anchorMin = new Vector2(0.03f, 0.63f);
            resolutionRect.anchorMax = new Vector2(0.96f, 0.72f);
            resolutionRect.offsetMin = Vector2.zero;
            resolutionRect.offsetMax = Vector2.zero;

            CreateResolutionButton(settingsObject.transform, "1280 x 720", 0, new Vector2(0.02f, 0.50f), new Vector2(0.44f, 0.60f));
            CreateResolutionButton(settingsObject.transform, "1600 x 900", 1, new Vector2(0.50f, 0.50f), new Vector2(0.94f, 0.60f));
            CreateResolutionButton(settingsObject.transform, "1920 x 1080", 2, new Vector2(0.02f, 0.38f), new Vector2(0.94f, 0.48f));

            var fullscreenButton = CreateButton("ПОЛНЫЙ ЭКРАН", settingsObject.transform, new Vector2(0.02f, 0.25f), new Vector2(0.94f, 0.35f));
            fullscreenButton.onClick.AddListener(ToggleFullscreen);
            RestyleCaseMenuButton(fullscreenButton, "ПОЛНЫЙ ЭКРАН", new Vector2(0.02f, 0.25f), new Vector2(0.94f, 0.35f));

            settingsFullscreenText = CreateText("Fullscreen Value", settingsObject.transform, 14, new Color(0.12f, 0.06f, 0.03f, 0.95f), TextAnchor.UpperLeft);
            var fullscreenRect = settingsFullscreenText.GetComponent<RectTransform>();
            fullscreenRect.anchorMin = new Vector2(0.03f, 0.16f);
            fullscreenRect.anchorMax = new Vector2(0.96f, 0.24f);
            fullscreenRect.offsetMin = Vector2.zero;
            fullscreenRect.offsetMax = Vector2.zero;

            var backButton = CreateButton("НАЗАД", settingsObject.transform, new Vector2(0.02f, 0.03f), new Vector2(0.50f, 0.13f));
            backButton.onClick.AddListener(() =>
            {
                audioController?.PlaySubmit();
                ShowMainMenuItems();
            });
            RestyleCaseMenuButton(backButton, "НАЗАД", new Vector2(0.02f, 0.03f), new Vector2(0.50f, 0.13f));
        }

        private void CreateResolutionButton(Transform parent, string label, int resolutionIndex, Vector2 anchorMin, Vector2 anchorMax)
        {
            var button = CreateButton(label, parent, anchorMin, anchorMax);
            button.onClick.AddListener(() => SetResolutionIndex(resolutionIndex));
            RestyleCaseMenuButton(button, label, anchorMin, anchorMax);
        }

        private void OpenSettingsMenu()
        {
            audioController?.PlaySubmit();
            menuItemsGroup.alpha = 0f;
            menuItemsGroup.interactable = false;
            menuItemsGroup.blocksRaycasts = false;
            menuSettingsGroup.alpha = 1f;
            menuSettingsGroup.interactable = true;
            menuSettingsGroup.blocksRaycasts = true;
            UpdateSettingsTexts();
        }

        private void ShowMainMenuItems()
        {
            if (menuSettingsGroup != null)
            {
                menuSettingsGroup.alpha = 0f;
                menuSettingsGroup.interactable = false;
                menuSettingsGroup.blocksRaycasts = false;
            }

            if (menuItemsGroup != null)
            {
                menuItemsGroup.alpha = 1f;
                menuItemsGroup.interactable = true;
                menuItemsGroup.blocksRaycasts = true;
            }
        }

        private void LoadDisplaySettings()
        {
            selectedResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionPrefKey, FindClosestResolutionIndex(Screen.width, Screen.height)), 0, supportedResolutions.Length - 1);
            selectedFullscreen = PlayerPrefs.GetInt(FullscreenPrefKey, Screen.fullScreen ? 1 : 0) == 1;
            ApplyDisplaySettings(false);
        }

        private void SetResolutionIndex(int index)
        {
            selectedResolutionIndex = Mathf.Clamp(index, 0, supportedResolutions.Length - 1);
            ApplyDisplaySettings(true);
        }

        private void ToggleFullscreen()
        {
            selectedFullscreen = !selectedFullscreen;
            ApplyDisplaySettings(true);
        }

        private void ApplyDisplaySettings(bool playSound)
        {
            var resolution = supportedResolutions[Mathf.Clamp(selectedResolutionIndex, 0, supportedResolutions.Length - 1)];
            Screen.SetResolution(resolution.x, resolution.y, selectedFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.SetInt(ResolutionPrefKey, selectedResolutionIndex);
            PlayerPrefs.SetInt(FullscreenPrefKey, selectedFullscreen ? 1 : 0);
            PlayerPrefs.Save();

            if (playSound)
            {
                audioController?.PlayTerminalBeep();
            }

            UpdateSettingsTexts();
        }

        private int FindClosestResolutionIndex(int width, int height)
        {
            var bestIndex = supportedResolutions.Length - 1;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < supportedResolutions.Length; i++)
            {
                var resolution = supportedResolutions[i];
                var distance = Mathf.Abs(resolution.x - width) + Mathf.Abs(resolution.y - height);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void UpdateSettingsTexts()
        {
            if (settingsResolutionText != null)
            {
                var resolution = supportedResolutions[Mathf.Clamp(selectedResolutionIndex, 0, supportedResolutions.Length - 1)];
                settingsResolutionText.text = $"РАЗРЕШЕНИЕ: {resolution.x} x {resolution.y}";
            }

            if (settingsFullscreenText != null)
            {
                settingsFullscreenText.text = selectedFullscreen ? "РЕЖИМ: ПОЛНЫЙ ЭКРАН" : "РЕЖИМ: ОКНО";
            }
        }

        private void CreateClosedFolderTitle()
        {
            var titleObject = new GameObject("Closed Folder Title");
            titleObject.transform.SetParent(mainMenuPanel.transform, false);
            var rect = titleObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.39f, 0.43f);
            rect.anchorMax = new Vector2(0.63f, 0.59f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, -1.8f);
            menuClosedTitleGroup = titleObject.AddComponent<CanvasGroup>();

            var title = CreateText("Title", titleObject.transform, 24, new Color(0.10f, 0.075f, 0.042f, 0.92f), TextAnchor.MiddleCenter);
            title.text = "AI INTERROGATION";

            var stamp = CreateText("Stamp", titleObject.transform, 14, new Color(0.24f, 0.035f, 0.02f, 0.78f), TextAnchor.LowerCenter);
            stamp.text = "ДЕЛО № 23-40";
        }

        private void CreateBriefingPanel()
        {
            briefingPanel = CreateFullscreenPanel("Briefing", new Color(0.015f, 0.013f, 0.01f, 0.68f));

            var title = CreateText("Briefing Text", briefingPanel.transform, 25, new Color(0.96f, 0.91f, 0.66f, 1f), TextAnchor.UpperLeft);
            briefingText = title;
            var rect = title.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.18f, 0.24f);
            rect.anchorMax = new Vector2(0.82f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = CreateButton("НАЧАТЬ ДОПРОС", briefingPanel.transform, new Vector2(0.41f, 0.10f), new Vector2(0.59f, 0.17f));
            button.onClick.AddListener(() => flow.StartInterrogation());
        }

        private void CreateTerminalPanel()
        {
            terminalPanel = new GameObject("Terminal Panel");
            terminalPanel.transform.SetParent(canvas.transform, false);
            terminalRect = terminalPanel.AddComponent<RectTransform>();
            terminalRect.anchorMin = new Vector2(0.018f, 0.045f);
            terminalRect.anchorMax = new Vector2(0.335f, 0.45f);
            terminalRect.offsetMin = Vector2.zero;
            terminalRect.offsetMax = Vector2.zero;
            terminalBasePosition = terminalRect.anchoredPosition;

            var background = terminalPanel.AddComponent<Image>();
            background.sprite = TextureFactory.CreateSolidSprite(Color.black);
            background.color = new Color(0f, 0f, 0f, 0.48f);
            RuntimeMaterialFactory.ApplyTo(background);

            suspicionText = CreateText("Suspicion", terminalPanel.transform, 16, new Color(1f, 0.72f, 0.36f, 1f), TextAnchor.MiddleLeft);
            var suspicionRect = suspicionText.GetComponent<RectTransform>();
            suspicionRect.anchorMin = new Vector2(0.04f, 0.91f);
            suspicionRect.anchorMax = new Vector2(0.96f, 0.98f);
            suspicionRect.offsetMin = Vector2.zero;
            suspicionRect.offsetMax = Vector2.zero;

            statusText = CreateText("Status", terminalPanel.transform, 15, new Color(0.55f, 1f, 0.46f, 1f), TextAnchor.MiddleRight);
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.04f, 0.91f);
            statusRect.anchorMax = new Vector2(0.96f, 0.98f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            CreateScrollLog();
            CreateInputArea();
        }

        private void CreateScrollLog()
        {
            var scrollObject = new GameObject("Dialog Log");
            scrollObject.transform.SetParent(terminalPanel.transform, false);
            var scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.04f, 0.23f);
            scrollRectTransform.anchorMax = new Vector2(0.96f, 0.89f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            scrollRect = scrollObject.AddComponent<ScrollRect>();
            var image = scrollObject.AddComponent<Image>();
            image.sprite = TextureFactory.CreateSolidSprite(Color.black);
            image.color = new Color(0f, 0f, 0f, 0.08f);
            RuntimeMaterialFactory.ApplyTo(image);
            var mask = scrollObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = new GameObject("Log Content");
            content.transform.SetParent(scrollObject.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 0f);
            contentRect.offsetMin = new Vector2(8f, 8f);
            contentRect.offsetMax = new Vector2(-8f, -8f);

            logText = content.AddComponent<Text>();
            logText.font = monoFont;
            logText.fontSize = 16;
            logText.supportRichText = true;
            logText.alignment = TextAnchor.LowerLeft;
            logText.color = new Color(0.72f, 1f, 0.58f, 1f);
            RuntimeMaterialFactory.ApplyToText(logText);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;

            scrollRect.content = contentRect;
            scrollRect.viewport = scrollRectTransform;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private void CreateInputArea()
        {
            waitingText = CreateText("Waiting", terminalPanel.transform, 16, new Color(1f, 0.72f, 0.36f, 1f), TextAnchor.MiddleLeft);
            var waitRect = waitingText.GetComponent<RectTransform>();
            waitRect.anchorMin = new Vector2(0.04f, 0.155f);
            waitRect.anchorMax = new Vector2(0.96f, 0.22f);
            waitRect.offsetMin = Vector2.zero;
            waitRect.offsetMax = Vector2.zero;

            var inputObject = new GameObject("Answer Input");
            inputObject.transform.SetParent(terminalPanel.transform, false);
            var inputRect = inputObject.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.04f, 0.05f);
            inputRect.anchorMax = new Vector2(0.86f, 0.15f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;

            var inputBackground = inputObject.AddComponent<Image>();
            inputBackground.sprite = TextureFactory.CreateSolidSprite(Color.black);
            inputBackground.color = new Color(0.02f, 0.025f, 0.018f, 0.62f);
            RuntimeMaterialFactory.ApplyTo(inputBackground);

            inputField = inputObject.AddComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 260;

            var text = CreateText("Input Text", inputObject.transform, 17, new Color(0.78f, 1f, 0.58f, 1f), TextAnchor.MiddleLeft);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            var placeholder = CreateText("Placeholder", inputObject.transform, 17, new Color(0.58f, 0.68f, 0.42f, 0.85f), TextAnchor.MiddleLeft);
            placeholder.text = "ответ...";
            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 0f);
            placeholderRect.offsetMax = new Vector2(-10f, 0f);

            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            var sendObject = new GameObject("Send Arrow Button");
            sendObject.transform.SetParent(terminalPanel.transform, false);
            var sendRect = sendObject.AddComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(0.875f, 0.05f);
            sendRect.anchorMax = new Vector2(0.96f, 0.15f);
            sendRect.offsetMin = Vector2.zero;
            sendRect.offsetMax = Vector2.zero;

            var sendImage = sendObject.AddComponent<Image>();
            sendImage.sprite = TextureFactory.CreateSolidSprite(Color.black);
            sendImage.color = new Color(0.05f, 0.07f, 0.035f, 0.72f);
            RuntimeMaterialFactory.ApplyTo(sendImage);

            sendButton = sendObject.AddComponent<Button>();
            var colors = sendButton.colors;
            colors.normalColor = new Color(0.05f, 0.07f, 0.035f, 0.72f);
            colors.highlightedColor = new Color(0.12f, 0.18f, 0.08f, 0.88f);
            colors.pressedColor = new Color(0.20f, 0.26f, 0.10f, 0.95f);
            colors.disabledColor = new Color(0.02f, 0.025f, 0.018f, 0.32f);
            colors.fadeDuration = 0.08f;
            sendButton.colors = colors;
            sendButton.onClick.AddListener(TrySubmitCurrentInput);

            var sendText = CreateText("Arrow", sendObject.transform, 20, new Color(0.82f, 1f, 0.56f, 1f), TextAnchor.MiddleCenter);
            sendText.text = ">";
        }

        private void CreateEvidencePanel()
        {
            evidencePanel = CreateFullscreenPanel("Evidence Rooms", Color.black);

            evidenceRoomOneSprite = LoadSprite("Evidence/room_1");
            evidenceRoomOneDoorOpenSprite = LoadSprite("Evidence/room_1_door_open");
            evidenceRoomTwoSprite = LoadSprite("Evidence/room_2");
            evidenceRoomTwoBriefcaseOpenSprite = LoadSprite("Evidence/room_2_briefcase_open");

            evidenceRoomImage = CreateFullscreenImage("Evidence Room Image", evidencePanel.transform, "Evidence/room_1");
            evidenceRoomImage.preserveAspect = true;

            var shade = new GameObject("Evidence Film Shade");
            shade.transform.SetParent(evidencePanel.transform, false);
            var shadeRect = shade.AddComponent<RectTransform>();
            shadeRect.StretchToParent();
            var shadeImage = shade.AddComponent<Image>();
            shadeImage.sprite = TextureFactory.CreateSolidSprite(Color.black);
            shadeImage.color = new Color(0f, 0f, 0f, 0.08f);
            RuntimeMaterialFactory.ApplyTo(shadeImage);
            shadeImage.raycastTarget = false;

            evidenceBackButton = CreateButton("<", evidencePanel.transform, new Vector2(0.024f, 0.90f), new Vector2(0.105f, 0.972f));
            evidenceBackButton.onClick.AddListener(HandleEvidenceBack);
            RestyleEvidenceButton(evidenceBackButton, "<");

            evidenceDoorButton = CreateEvidenceHotspot("Door Hotspot", new Vector2(0.56f, 0.18f), new Vector2(0.84f, 0.82f));
            var doorHotspot = evidenceDoorButton.gameObject.AddComponent<EvidenceHotspot>();
            doorHotspot.PointerEntered = HandleDoorHoverEnter;
            doorHotspot.PointerExited = HandleDoorHoverExit;
            doorHotspot.PointerPressed = () => SetEvidenceHint("проход...");
            doorHotspot.PointerReleased = () => SetEvidenceHint("дверь открыта. нажмите, чтобы пройти");
            doorHotspot.PointerClicked = EnterSecondEvidenceRoom;

            evidenceBriefcaseButton = CreateEvidenceHotspot("Briefcase Hotspot", new Vector2(0.04f, 0.15f), new Vector2(0.58f, 0.82f));
            var briefcaseHotspot = evidenceBriefcaseButton.gameObject.AddComponent<EvidenceHotspot>();
            briefcaseHotspot.PointerEntered = () => SetEvidenceHint("портфель. нажмите, чтобы открыть");
            briefcaseHotspot.PointerExited = () => SetEvidenceHint("осмотрите комнату");
            briefcaseHotspot.PointerPressed = () => SetEvidenceHint("открываю...");
            briefcaseHotspot.PointerReleased = () => SetEvidenceHint("портфель открыт");
            briefcaseHotspot.PointerClicked = OpenBriefcaseEvidence;

            evidenceHintText = CreateText("Evidence Hint", evidencePanel.transform, 18, new Color(0.95f, 0.72f, 0.36f, 0.92f), TextAnchor.LowerCenter);
            var hintRect = evidenceHintText.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.18f, 0.03f);
            hintRect.anchorMax = new Vector2(0.82f, 0.11f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            evidencePanel.SetActive(false);
        }

        private Button CreateEvidenceHotspot(string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(evidencePanel.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = obj.AddComponent<Image>();
            image.sprite = TextureFactory.CreateSolidSprite(Color.white);
            image.color = new Color(0.95f, 0.72f, 0.36f, 0.01f);
            RuntimeMaterialFactory.ApplyTo(image);

            var button = obj.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.95f, 0.72f, 0.36f, 0.01f);
            colors.highlightedColor = new Color(0.95f, 0.72f, 0.36f, 0.12f);
            colors.pressedColor = new Color(1f, 0.82f, 0.42f, 0.20f);
            colors.selectedColor = new Color(0.95f, 0.72f, 0.36f, 0.08f);
            colors.disabledColor = new Color(0f, 0f, 0f, 0f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private void RestyleEvidenceButton(Button button, string label)
        {
            var image = button.GetComponent<Image>();
            image.color = new Color(0.02f, 0.018f, 0.012f, 0.58f);

            var colors = button.colors;
            colors.normalColor = new Color(0.02f, 0.018f, 0.012f, 0.58f);
            colors.highlightedColor = new Color(0.16f, 0.11f, 0.05f, 0.76f);
            colors.pressedColor = new Color(0.28f, 0.18f, 0.08f, 0.86f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0f, 0f, 0f, 0.2f);
            colors.fadeDuration = 0.10f;
            button.colors = colors;

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = 25;
                text.color = new Color(0.98f, 0.74f, 0.38f, 0.96f);
            }
        }

        private void ShowEvidenceFirstRoom()
        {
            evidenceRoomState = EvidenceRoomState.FirstRoom;
            SetEvidenceSprite(evidenceRoomOneSprite);
            evidenceDoorButton.gameObject.SetActive(true);
            evidenceBriefcaseButton.gameObject.SetActive(false);
            SetEvidenceHint("доступ к уликам открыт. наведите на дверь");
        }

        private void ShowEvidenceSecondRoom(bool briefcaseOpen)
        {
            evidenceRoomState = briefcaseOpen ? EvidenceRoomState.BriefcaseOpen : EvidenceRoomState.SecondRoom;
            SetEvidenceSprite(briefcaseOpen ? evidenceRoomTwoBriefcaseOpenSprite : evidenceRoomTwoSprite);
            evidenceDoorButton.gameObject.SetActive(false);
            evidenceBriefcaseButton.gameObject.SetActive(!briefcaseOpen);
            SetEvidenceHint(briefcaseOpen ? "портфель открыт" : "осмотрите комнату");
        }

        private void HandleDoorHoverEnter()
        {
            if (evidenceRoomState != EvidenceRoomState.FirstRoom)
            {
                return;
            }

            SetEvidenceSprite(evidenceRoomOneDoorOpenSprite);
            SetEvidenceHint("дверь открыта. нажмите, чтобы пройти");
            audioController?.PlayDoorHover();
        }

        private void HandleDoorHoverExit()
        {
            if (evidenceRoomState != EvidenceRoomState.FirstRoom)
            {
                return;
            }

            SetEvidenceSprite(evidenceRoomOneSprite);
            SetEvidenceHint("доступ к уликам открыт. наведите на дверь");
            audioController?.PlayDoorClose();
        }

        private void EnterSecondEvidenceRoom()
        {
            if (evidenceRoomState != EvidenceRoomState.FirstRoom)
            {
                return;
            }

            audioController?.PlaySubmit();
            ShowEvidenceSecondRoom(false);
        }

        private void OpenBriefcaseEvidence()
        {
            if (evidenceRoomState != EvidenceRoomState.SecondRoom)
            {
                return;
            }

            audioController?.PlayBriefcaseOpen();
            ShowEvidenceSecondRoom(true);
        }

        private void HandleEvidenceBack()
        {
            audioController?.PlaySubmit();
            HideEvidenceRoomToInterrogation();
        }

        private void SetEvidenceSprite(Sprite sprite)
        {
            if (evidenceRoomImage == null)
            {
                return;
            }

            evidenceRoomImage.sprite = sprite != null ? sprite : TextureFactory.CreateSolidSprite(Color.black);
        }

        private void SetEvidenceHint(string text)
        {
            if (evidenceHintText != null)
            {
                evidenceHintText.text = text ?? string.Empty;
            }
        }

        private void CreateFinalPanel()
        {
            finalPanel = CreateFullscreenPanel("Final Report", new Color(0.005f, 0.004f, 0.003f, 0.68f));
            finalText = CreateText("Final Text", finalPanel.transform, 22, new Color(0.96f, 0.94f, 0.68f, 1f), TextAnchor.UpperLeft);
            var rect = finalText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.22f, 0.18f);
            rect.anchorMax = new Vector2(0.78f, 0.82f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = CreateButton("НОВЫЙ ДОПРОС", finalPanel.transform, new Vector2(0.42f, 0.08f), new Vector2(0.58f, 0.15f));
            button.onClick.AddListener(() => flow.RestartGame());
        }

        private void CreateQuickMenuButton()
        {
            var button = CreateButton("МЕНЮ", canvas.transform, new Vector2(0.875f, 0.925f), new Vector2(0.985f, 0.985f));
            quickMenuButton = button.gameObject;
            button.onClick.AddListener(() => flow.ReturnToMainMenu());
            quickMenuButton.SetActive(false);
        }

        private void CreateEvidenceAccessButton()
        {
            var button = CreateButton("посмотреть улики", canvas.transform, new Vector2(0.018f, 0.925f), new Vector2(0.195f, 0.985f));
            evidenceAccessButton = button.gameObject;
            button.onClick.AddListener(() => flow.OpenEvidenceFromButton());
            RestyleEvidenceAccessButton(button);
            evidenceAccessButton.SetActive(false);
        }

        private void RestyleEvidenceAccessButton(Button button)
        {
            var image = button.GetComponent<Image>();
            image.color = new Color(0.02f, 0.025f, 0.018f, 0.62f);

            var colors = button.colors;
            colors.normalColor = new Color(0.02f, 0.025f, 0.018f, 0.62f);
            colors.highlightedColor = new Color(0.10f, 0.14f, 0.07f, 0.82f);
            colors.pressedColor = new Color(0.18f, 0.23f, 0.09f, 0.92f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.02f, 0.025f, 0.018f, 0.28f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = "посмотреть улики";
                text.fontSize = 15;
                text.color = new Color(0.82f, 1f, 0.56f, 1f);
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        private GameObject CreateFullscreenPanel(string name, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(canvas.transform, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.StretchToParent();
            var image = panel.AddComponent<Image>();
            image.sprite = TextureFactory.CreateSolidSprite(Color.black);
            image.color = color;
            RuntimeMaterialFactory.ApplyTo(image);
            return panel;
        }

        private Sprite LoadSprite(string resourcesPath)
        {
            var texture = Resources.Load<Texture2D>(resourcesPath);
            if (texture == null)
            {
                Debug.LogWarning($"Missing sprite texture at Resources/{resourcesPath}.");
                return TextureFactory.CreateSolidSprite(Color.black);
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Image CreateFullscreenImage(string name, Transform parent, string resourcesPath)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.StretchToParent();

            var image = obj.AddComponent<Image>();
            RuntimeMaterialFactory.ApplyTo(image);
            image.sprite = LoadSprite(resourcesPath);

            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private RawImage CreateFullscreenVideo(string name, Transform parent, string resourcesPath)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.StretchToParent();

            var raw = obj.AddComponent<RawImage>();
            RuntimeMaterialFactory.ApplyTo(raw);
            raw.color = Color.white;
            raw.raycastTarget = false;
            raw.gameObject.SetActive(false);

            menuVideoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "Main Menu Video Texture"
            };
            menuVideoTexture.Create();
            raw.texture = menuVideoTexture;

            var videoObject = new GameObject("Main Menu Video Player");
            videoObject.transform.SetParent(parent, false);
            menuVideoPlayer = videoObject.AddComponent<VideoPlayer>();
            menuVideoPlayer.clip = Resources.Load<VideoClip>(resourcesPath);
            menuVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            menuVideoPlayer.targetTexture = menuVideoTexture;
            menuVideoPlayer.playOnAwake = false;
            menuVideoPlayer.isLooping = false;
            menuVideoPlayer.waitForFirstFrame = true;
            menuVideoPlayer.skipOnDrop = false;
            menuVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            menuVideoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            menuVideoPlayer.sendFrameReadyEvents = false;

            if (menuVideoPlayer.clip == null)
            {
                Debug.LogWarning($"Main menu video is missing from Resources/{resourcesPath}.");
                raw.gameObject.SetActive(false);
            }

            return raw;
        }

        private Text CreateText(string name, Transform parent, int fontSize, Color color, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = obj.AddComponent<Text>();
            text.font = monoFont;
            text.fontSize = fontSize;
            text.color = color;
            RuntimeMaterialFactory.ApplyToText(text);
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(string label, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(label + " Button");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = obj.AddComponent<Image>();
            image.sprite = TextureFactory.CreateSolidSprite(Color.black);
            image.color = new Color(0.03f, 0.04f, 0.025f, 0.92f);
            RuntimeMaterialFactory.ApplyTo(image);

            var button = obj.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.10f, 0.14f, 0.08f, 0.95f);
            colors.pressedColor = new Color(0.16f, 0.20f, 0.10f, 1f);
            button.colors = colors;

            var buttonText = CreateText("Label", obj.transform, 17, new Color(0.72f, 1f, 0.58f, 1f), TextAnchor.MiddleCenter);
            buttonText.text = label;
            return button;
        }

        private void RestyleCaseMenuButton(Button button, string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = button.GetComponent<Image>();
            image.color = new Color(0.74f, 0.61f, 0.38f, 0.34f);

            var outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.11f, 0.07f, 0.035f, 0.38f);
            outline.effectDistance = new Vector2(1f, -1f);

            var colors = button.colors;
            colors.normalColor = new Color(0.74f, 0.61f, 0.38f, 0.34f);
            colors.highlightedColor = new Color(0.86f, 0.70f, 0.43f, 0.46f);
            colors.pressedColor = new Color(0.48f, 0.31f, 0.18f, 0.58f);
            colors.selectedColor = new Color(0.80f, 0.62f, 0.34f, 0.50f);
            colors.disabledColor = new Color(0.28f, 0.24f, 0.18f, 0.24f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var labelText = button.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                labelText.fontSize = 17;
                labelText.alignment = TextAnchor.MiddleLeft;
                labelText.color = new Color(0.09f, 0.055f, 0.032f, 0.96f);
                var labelRect = labelText.GetComponent<RectTransform>();
                labelRect.offsetMin = new Vector2(34f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);
            }

            var marker = new GameObject("Archive Marker");
            marker.transform.SetParent(button.transform, false);
            var markerRect = marker.AddComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0.18f);
            markerRect.anchorMax = new Vector2(0f, 0.82f);
            markerRect.sizeDelta = new Vector2(5f, 0f);
            markerRect.anchoredPosition = new Vector2(14f, 0f);
            var markerImage = marker.AddComponent<Image>();
            markerImage.sprite = TextureFactory.CreateSolidSprite(Color.white);
            markerImage.color = new Color(0.28f, 0.035f, 0.02f, 0.74f);
            RuntimeMaterialFactory.ApplyTo(markerImage);
            markerImage.raycastTarget = false;
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
