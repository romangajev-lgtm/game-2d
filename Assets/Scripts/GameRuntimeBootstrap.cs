using UnityEngine;

namespace AIInterrogation
{
    public static class GameRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindObjectOfType<GameFlowController>() != null)
            {
                Object.FindObjectOfType<GameFlowController>().InitializeRuntime();
                return;
            }

            var root = new GameObject("AI Interrogation Runtime");
            Object.DontDestroyOnLoad(root);
            var flow = root.AddComponent<GameFlowController>();
            flow.InitializeRuntime();
        }
    }
}
