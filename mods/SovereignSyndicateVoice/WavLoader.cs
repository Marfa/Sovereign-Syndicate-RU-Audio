using System;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    internal sealed class LoadedWav
    {
        private readonly float[] _samples;
        private readonly int _channels;
        private int _position;

        private LoadedWav(float[] samples, int channels, int sampleRate, string clipName)
        {
            _samples = samples;
            _channels = channels;
            var frameCount = channels > 0 ? samples.Length / channels : samples.Length;
            Clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, true, OnAudioRead, OnAudioSetPosition);
        }

        internal AudioClip Clip { get; }

        internal float DurationSeconds
        {
            get
            {
                if (_channels <= 0)
                {
                    return 0f;
                }

                return (float)(_samples.Length / _channels) / Clip.frequency;
            }
        }

        internal static LoadedWav TryLoad(string path)
        {
            try
            {
                return Load(path);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("WAV parse error: " + path + " — " + ex.Message);
                return null;
            }
        }

        private static LoadedWav Load(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (new string(reader.ReadChars(4)) != "RIFF")
            {
                throw new InvalidDataException("not RIFF");
            }

            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
            {
                throw new InvalidDataException("not WAVE");
            }

            var channels = 0;
            var sampleRate = 0;
            var bitsPerSample = 0;
            byte[] data = null;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadInt32();
                if (chunkId == "fmt ")
                {
                    reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    var remaining = chunkSize - 16;
                    if (remaining > 0)
                    {
                        reader.ReadBytes(remaining);
                    }
                }
                else if (chunkId == "data")
                {
                    data = reader.ReadBytes(chunkSize);
                    break;
                }
                else
                {
                    reader.ReadBytes(chunkSize);
                }
            }

            if (data == null || channels <= 0 || sampleRate <= 0 || bitsPerSample != 16)
            {
                throw new InvalidDataException("unsupported wav format");
            }

            var sampleCount = data.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = BitConverter.ToInt16(data, i * 2);
                samples[i] = sample / 32768f;
            }

            var clipName = Path.GetFileNameWithoutExtension(path);
            return new LoadedWav(samples, channels, sampleRate, clipName);
        }

        private void OnAudioRead(float[] data)
        {
            var written = 0;
            while (written < data.Length)
            {
                if (_position >= _samples.Length)
                {
                    for (var i = written; i < data.Length; i++)
                    {
                        data[i] = 0f;
                    }

                    return;
                }

                data[written] = _samples[_position];
                _position++;
                written++;
            }
        }

        private void OnAudioSetPosition(int newPosition)
        {
            _position = Mathf.Clamp(newPosition * _channels, 0, _samples.Length);
        }
    }
}
