using System.Collections.Generic;
using UnityEngine;

namespace Keske
{
    public class KeskeAudio : MonoBehaviour
    {
        AudioSource effects;
        AudioSource ambience;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioClip> generated = new List<AudioClip>();

        public void Initialize(KeskeGame game)
        {
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.spatialBlend = 0;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.loop = true;
            ambience.spatialBlend = 0;
            clips["step"] = Choose(game.stepClip, "step", 0.13f, 90, 0.35f);
            clips["pickup"] = Choose(game.pickupClip, "pickup", 0.2f, 640, 0.02f);
            clips["done"] = Choose(game.successClip, "done", 0.45f, 420, 0.01f);
            clips["door"] = Choose(game.doorClip, "door", 0.4f, 120, 0.3f);
            clips["switch"] = Choose(game.switchClip, "switch", 0.25f, 440, 0.01f);
            if (game.ambienceClip != null)
            { ambience.clip = game.ambienceClip; ambience.volume = 0.08f; ambience.Play(); }
        }

        AudioClip Choose(AudioClip custom, string name, float seconds, float hz, float noise, bool loop = false)
        {
            if (custom != null) return custom;
            var clip = Make(name, seconds, hz, noise, loop);
            generated.Add(clip);
            return clip;
        }

        static AudioClip Make(string name, float seconds, float hz, float noise, bool loop = false)
        {
            const int rate = 22050;
            int length = (int)(seconds * rate);
            float[] samples = new float[length];
            var random = new System.Random(1247 + length);
            float smooth = 0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float p = (float)i / length;
                smooth = smooth * 0.9f + ((float)random.NextDouble() * 2 - 1) * 0.1f;
                float envelope = loop ? Mathf.Sin(Mathf.PI * p) : Mathf.Min(t * 40, 1) * Mathf.Pow(1 - p, 2);
                float tone = Mathf.Sin(2 * Mathf.PI * hz * t) * 0.13f + Mathf.Sin(2 * Mathf.PI * hz * 1.5f * t) * 0.04f;
                samples[i] = (tone + smooth * noise) * envelope;
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Play(string id, float volume = 0.65f)
        {
            if (effects != null && clips.ContainsKey(id)) effects.PlayOneShot(clips[id], volume);
        }
        public void SetPreview(bool preview) { if (ambience != null) ambience.pitch = 1; }
        void OnDestroy() { foreach (var clip in generated) if (clip != null) Destroy(clip); }
    }
}
