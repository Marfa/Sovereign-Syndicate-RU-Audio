using System.IO;
using FMOD;
using FMODUnity;
using MelonLoader;

namespace SovereignSyndicateVoice
{
    internal static class FmodVoicePlayer
    {
        private static Channel _channel;
        private static Sound _sound;
        private static string _lastPath = string.Empty;
        private static float _lastPlayTime;

        internal static bool TryPlay(string path, out float durationSeconds)
        {
            durationSeconds = 0f;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            if (RuntimeManager.IsMuted)
            {
                MelonLogger.Warning("FMOD RuntimeManager.IsMuted=true");
            }

            var now = UnityEngine.Time.unscaledTime;
            Stop();

            var system = RuntimeManager.CoreSystem;
            var mode = MODE.LOOP_OFF | MODE._2D | MODE.CREATESAMPLE;
            var result = system.createSound(path, mode, out _sound);
            if (result != RESULT.OK)
            {
                MelonLogger.Warning("FMOD createSound failed: " + result + " path=" + path);
                ReleaseSound();
                return false;
            }

            _sound.getLength(out var lengthMs, TIMEUNIT.MS);
            durationSeconds = lengthMs / 1000f;

            result = system.getMasterChannelGroup(out var master);
            if (result != RESULT.OK)
            {
                MelonLogger.Warning("FMOD getMasterChannelGroup failed: " + result);
                ReleaseSound();
                return false;
            }

            result = system.playSound(_sound, master, false, out _channel);
            if (result != RESULT.OK || !_channel.hasHandle())
            {
                MelonLogger.Warning("FMOD playSound failed: " + result + " path=" + path);
                ReleaseSound();
                return false;
            }

            _channel.setVolume(1f);
            _channel.setPaused(false);
            _channel.isPlaying(out var playing);
            if (!playing)
            {
                MelonLogger.Warning("FMOD channel not playing after playSound: " + path);
                Stop();
                return false;
            }

            _lastPath = path;
            _lastPlayTime = now;
            MelonLogger.Msg("FMOD ok: playing=" + playing + " dur=" + durationSeconds.ToString("F1") + "s");
            return true;
        }

        internal static bool IsPlaying
        {
            get
            {
                if (!_channel.hasHandle())
                {
                    return false;
                }

                _channel.isPlaying(out var playing);
                return playing;
            }
        }

        internal static void Stop()
        {
            if (_channel.hasHandle())
            {
                _channel.stop();
                _channel.clearHandle();
            }

            ReleaseSound();
            _lastPath = string.Empty;
            _lastPlayTime = 0f;
        }

        private static void ReleaseSound()
        {
            if (_sound.hasHandle())
            {
                _sound.release();
                _sound.clearHandle();
            }
        }
    }
}
