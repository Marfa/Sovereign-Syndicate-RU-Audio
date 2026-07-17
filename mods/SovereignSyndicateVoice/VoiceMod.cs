using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SovereignSyndicateVoice.VoiceMod), "Sovereign Syndicate Voice", "0.5.27", "themarfa")]
[assembly: MelonGame("Crimson Herring Studios", "Sovereign Syndicate")]

namespace SovereignSyndicateVoice
{
    public sealed class VoiceMod : MelonMod
    {
        internal static VoiceMod Instance { get; private set; }
        internal static VoicePlayer Player { get; private set; }
        internal static VoiceAudioHost AudioHost { get; private set; }

        private HarmonyLib.Harmony _harmony;

        public override void OnInitializeMelon()
        {
            Instance = this;
            VoiceEnvBootstrap.Run();
            VoiceSettings.LoadOrCreate();
            Player = new VoicePlayer(VoicePaths.VoiceRoot);
            EnsureAudioHost();

            _harmony = new HarmonyLib.Harmony("sovereign.syndicate.voice");
            _harmony.PatchAll(typeof(VoiceMod).Assembly);

            MelonLogger.Msg("Voice root: " + Player.VoiceRoot);
            MelonLogger.Msg("VO via FMOD (game audio bus)");
            MelonCoroutines.Start(SubscribeDialogueHooksLoop());
        }

        private static IEnumerator SubscribeDialogueHooksLoop()
        {
            for (var i = 0; i < 120; i++)
            {
                DialogueDumper.TryDump();
                DialogueHooks.TrySubscribe();
                if (DialogueHooks.IsSubscribed)
                {
                    yield break;
                }

                yield return new WaitForSeconds(1f);
            }
        }

        private static string _lastSceneKey = string.Empty;
        private static float _lastSceneLoadTime;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var sceneKey = buildIndex + ":" + (sceneName ?? string.Empty);
            var now = Time.unscaledTime;
            if (sceneKey != _lastSceneKey || now - _lastSceneLoadTime >= 1.5f)
            {
                _lastSceneKey = sceneKey;
                _lastSceneLoadTime = now;
                VoicePendingReplay.Cancel();
                VoicePrefetch.OnSceneLoaded();
            }

            EnsureAudioHost();
            DialogueHooks.ResetForSceneLoad();
            DialogueHooks.TrySubscribe();
        }

        internal static VoiceAudioHost EnsureAudioHost()
        {
            if (AudioHost != null)
            {
                return AudioHost;
            }

            var hostGo = new GameObject("SyndicateVoiceHost");
            UnityEngine.Object.DontDestroyOnLoad(hostGo);
            AudioHost = hostGo.AddComponent<VoiceAudioHost>();
            return AudioHost;
        }

        public override void OnUpdate()
        {
            VoicePrefetch.Tick();
        }

        public override void OnApplicationQuit()
        {
            VoicePrefetch.ForceShutdown("game exit");
            Player?.StopAllVo();
            FmodVoicePlayer.Stop();

            if (VoiceSettings.DeleteWavOnExit)
            {
                VoiceCacheCleanup.PurgeSessionWavs(VoicePaths.VoiceRoot);
            }
            else
            {
                MelonLogger.Msg("VO cleanup skipped (delete_wav_on_exit=false)");
            }

            if (AudioHost != null)
            {
                UnityEngine.Object.Destroy(AudioHost.gameObject);
                AudioHost = null;
            }
        }
    }
}
