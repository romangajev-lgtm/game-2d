# AI Interrogation 2D MVP

Unity MVP for a static 2D psychological interrogation game. The runtime scene is built by `GameRuntimeBootstrap`, so opening `Assets/Scenes/Interrogation.unity` and pressing Play is enough.

## Run

1. Open this folder in Unity Hub as a Unity project.
2. Open `Assets/Scenes/Interrogation.unity`.
3. Press Play.

The project uses the two provided images:

- `Assets/Resources/Art/interrogator_calm.png`
- `Assets/Resources/Art/interrogator_angry.png`

## OpenAI config

The game never stores an API key in code. To enable live AI replies, copy `LocalConfig/openai.local.example.json` to `LocalConfig/openai.local.json` and put your key there. `LocalConfig/openai.local.json` is ignored by git. If the file is missing or the network/API call fails, the game falls back to `MockAIClient` and prints the error in the terminal UI.

The case `truth` is shown in briefing and used by local analysis only. It is not sent to the AI interrogator.

## Tests

Use Unity Test Runner and run EditMode tests under `Assets/Tests`.

## Audio

Runtime audio is managed by `Assets/Scripts/AudioController.cs`. The generated WAV assets live in `Assets/Resources/Audio`:

- `room_ambience.wav` - looping low room tone.
- `lamp_buzz.wav` - looping electrical lamp buzz plus rare flicker ticks.
- `type_click.wav` - typewriter/UI click during investigator text.
- `input_submit.wav` - short confirmation when the player sends an answer.
- `folder_open.wav` - soft paper/folder movement for the main menu opening transition.
- `terminal_beep.wav` - short AI/mock response cue.
- `anger_hit.wav` - low hit when the investigator switches to angry pressure.
- `table_slam.wav` - heavier desk hit used by the timed anger animation.
- `final_sting.wav` - final report sting.
