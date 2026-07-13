using System;

using System.Collections;

using System.IO;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



[assembly: MelonInfo(typeof(SovereignSyndicateVoice.VoiceMod), "Sovereign Syndicate Voice", "0.5.13", "themarfa")]

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

            Player = new VoicePlayer(ResolveVoiceRoot());

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

            if (Player != null)
            {
                VoiceCacheCleanup.PurgeSessionWavs(Player.VoiceRoot);
            }

            Player?.Stop();
            FmodVoicePlayer.Stop();

            if (AudioHost != null)

            {

                UnityEngine.Object.Destroy(AudioHost.gameObject);

                AudioHost = null;

            }

        }



        private static string ResolveVoiceRoot()

        {

            var gameDir = Path.GetDirectoryName(Application.dataPath);

            var modsDir = Path.Combine(gameDir, "Mods", "SovereignSyndicateVoice", "voice");

            if (Directory.Exists(modsDir))

            {

                return modsDir;

            }



            var tempDir = @"C:\Temp\SovereignSyndicateVoice\voice";

            if (Directory.Exists(tempDir))

            {

                MelonLogger.Msg("Using dev voice folder: " + tempDir);

                return tempDir;

            }



            Directory.CreateDirectory(modsDir);

            return modsDir;

        }

    }

}


