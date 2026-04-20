using UnityEngine;

namespace AIInterrogation
{
    public class AudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip folderOpenClip;
        [SerializeField] private AudioClip[] folderOpenVariants;
        [SerializeField] private string folderOpenResourcePath = "Audio/folder_open";
        [SerializeField] private string briefcaseOpenResourcePath = "Audio/briefcase_open";
        [SerializeField, Range(0f, 1f)] private float folderOpenVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float briefcaseOpenVolume = 0.26f;
        [SerializeField, Range(0f, 0.25f)] private float folderOpenPitchVariation = 0.07f;

        private AudioSource ambienceSource;
        private AudioSource loopAccentSource;
        private AudioSource sfxSource;
        private AudioSource uiSource;

        private AudioClip roomAmbience;
        private AudioClip lampBuzz;
        private AudioClip typeClick;
        private AudioClip inputSubmit;
        private AudioClip angerHit;
        private AudioClip tableSlam;
        private AudioClip finalSting;
        private AudioClip terminalBeep;
        private AudioClip briefcaseOpen;

        private float nextTypeClickTime;
        private float nextLampFlickerTime;
        private float nextFolderOpenTime;
        private float nextBriefcaseOpenTime;

        public void Initialize()
        {
            if (ambienceSource != null)
            {
                return;
            }

            roomAmbience = Resources.Load<AudioClip>("Audio/room_ambience");
            lampBuzz = Resources.Load<AudioClip>("Audio/lamp_buzz");
            typeClick = Resources.Load<AudioClip>("Audio/type_click");
            inputSubmit = Resources.Load<AudioClip>("Audio/input_submit");
            angerHit = Resources.Load<AudioClip>("Audio/anger_hit");
            tableSlam = Resources.Load<AudioClip>("Audio/table_slam");
            finalSting = Resources.Load<AudioClip>("Audio/final_sting");
            terminalBeep = Resources.Load<AudioClip>("Audio/terminal_beep");
            briefcaseOpen = Resources.Load<AudioClip>(briefcaseOpenResourcePath);
            if (folderOpenClip == null && !string.IsNullOrWhiteSpace(folderOpenResourcePath))
            {
                folderOpenClip = Resources.Load<AudioClip>(folderOpenResourcePath);
            }

            ambienceSource = CreateSource("Audio Ambience", 0.16f, true);
            loopAccentSource = CreateSource("Audio Lamp Buzz", 0.035f, true);
            sfxSource = CreateSource("Audio SFX", 0.55f, false);
            uiSource = CreateSource("Audio UI", 0.28f, false);

            if (roomAmbience != null)
            {
                ambienceSource.clip = roomAmbience;
                ambienceSource.Play();
            }

            if (lampBuzz != null)
            {
                loopAccentSource.clip = lampBuzz;
                loopAccentSource.Play();
            }
        }

        public void PlayTypeClick()
        {
            if (Time.unscaledTime < nextTypeClickTime)
            {
                return;
            }

            nextTypeClickTime = Time.unscaledTime + 0.035f;
            Play(uiSource, typeClick, Random.Range(0.08f, 0.14f), Random.Range(0.86f, 1.08f));
        }

        public void PlaySubmit()
        {
            Play(uiSource, inputSubmit, 0.34f, Random.Range(0.92f, 1.04f));
        }

        public void PlayFolderOpen()
        {
            if (Time.unscaledTime < nextFolderOpenTime)
            {
                return;
            }

            nextFolderOpenTime = Time.unscaledTime + 0.75f;
            var clip = ChooseFolderOpenClip();
            var pitch = Random.Range(1f - folderOpenPitchVariation, 1f + folderOpenPitchVariation);
            Play(uiSource, clip, folderOpenVolume, pitch);
        }

        public void PlayBriefcaseOpen()
        {
            if (Time.unscaledTime < nextBriefcaseOpenTime)
            {
                return;
            }

            nextBriefcaseOpenTime = Time.unscaledTime + 0.55f;
            var clip = briefcaseOpen != null ? briefcaseOpen : ChooseFolderOpenClip();
            Play(uiSource, clip, briefcaseOpenVolume, Random.Range(0.96f, 1.04f));
        }

        public void PlayTerminalBeep()
        {
            Play(uiSource, terminalBeep, 0.20f, Random.Range(0.92f, 1.08f));
        }

        public void PlayAngerHit()
        {
            Play(sfxSource, angerHit, 0.62f, Random.Range(0.93f, 1.02f));
        }

        public void PlayTableSlam()
        {
            Play(sfxSource, tableSlam != null ? tableSlam : angerHit, 0.78f, Random.Range(0.94f, 1.03f));
        }

        public void PlayFinalSting()
        {
            Play(sfxSource, finalSting, 0.72f, 1f);
        }

        private void Update()
        {
            if (lampBuzz == null || uiSource == null)
            {
                return;
            }

            if (Time.unscaledTime < nextLampFlickerTime)
            {
                return;
            }

            nextLampFlickerTime = Time.unscaledTime + Random.Range(5.0f, 11.0f);
            Play(uiSource, lampBuzz, 0.018f, Random.Range(0.95f, 1.08f));
        }

        private AudioSource CreateSource(string sourceName, float volume, bool loop)
        {
            var obj = new GameObject(sourceName);
            obj.transform.SetParent(transform, false);

            var source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        private AudioClip ChooseFolderOpenClip()
        {
            if (folderOpenVariants != null && folderOpenVariants.Length > 0)
            {
                var clip = folderOpenVariants[Random.Range(0, folderOpenVariants.Length)];
                if (clip != null)
                {
                    return clip;
                }
            }

            return folderOpenClip;
        }

        private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (source == null || clip == null)
            {
                return;
            }

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
