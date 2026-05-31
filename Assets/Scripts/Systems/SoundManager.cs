using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Galatea.Data;

namespace Galatea.Systems
{
    // Single-instance audio dispatcher. Missing instance or missing clip is a
    // silent no-op for every static call. Final volume per SFX is
    // perClipVolume × categoryVolume × masterSfxVolume. SFX play through a pooled
    // 2D AudioSource via PlayOneShot; cooking and walking loops use dedicated
    // looping sources.
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;

        [Serializable]
        public class SoundClip
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Header("Background Music")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
        [SerializeField] private bool loopMusic = true;

        [Header("Master SFX")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Per-Category Volumes")]
        [SerializeField, Range(0f, 1f)] private float cookingVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float creatureVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float playerVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float ingredientVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float platingVolume = 1f;

        [Header("Cooking Sounds")]
        [SerializeField] private SoundClip ingredientChopped = new SoundClip();
        [Tooltip("Looped while a DryHeat slot is processing. Use a seamlessly-looping clip.")]
        [FormerlySerializedAs("sauteeTick")]
        [SerializeField] private SoundClip sauteeLoop        = new SoundClip();
        [Tooltip("Looped while a WetHeat slot is processing. Use a seamlessly-looping clip.")]
        [FormerlySerializedAs("boilTick")]
        [SerializeField] private SoundClip boilLoop          = new SoundClip();
        [SerializeField] private SoundClip slotProcessOn     = new SoundClip();
        [SerializeField] private SoundClip slotProcessOff    = new SoundClip();

        [Header("Creature Sounds")]
        [SerializeField] private SoundClip creatureFed         = new SoundClip();
        [SerializeField] private SoundClip creatureCozy        = new SoundClip();
        [SerializeField] private SoundClip creatureRefreshed   = new SoundClip();
        [SerializeField] private SoundClip creatureSpicy       = new SoundClip();
        [SerializeField] private SoundClip creatureConfused    = new SoundClip();
        [SerializeField] private SoundClip creatureDisappointed = new SoundClip();
        [SerializeField] private SoundClip creatureDisgusted   = new SoundClip();
        [SerializeField] private SoundClip creatureDelighted   = new SoundClip();

        [Header("Player Sounds")]
        [Tooltip("Looped while the player is moving. Use a seamlessly-looping walk clip.")]
        [FormerlySerializedAs("footstep")]
        [SerializeField] private SoundClip walkLoop = new SoundClip { volume = 0.4f };

        [Header("Ingredient Sounds")]
        [SerializeField] private SoundClip ingredientSpawned   = new SoundClip();
        [SerializeField] private SoundClip ingredientPickedUp  = new SoundClip();
        [SerializeField] private SoundClip ingredientDropped   = new SoundClip();

        [Header("Plating Sounds")]
        [SerializeField] private SoundClip plateSpawned      = new SoundClip();
        [SerializeField] private SoundClip itemPlacedOnPlate = new SoundClip();

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _walkSource;

        // Active per-slot cooking loops, keyed by StationSlot.GetInstanceID() so each
        // slot can independently start/stop its own loop.
        private readonly Dictionary<int, AudioSource> _activeLoops = new Dictionary<int, AudioSource>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.spatialBlend = 0f;
            _musicSource.loop = loopMusic;
            _musicSource.volume = musicVolume;
            _musicSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.spatialBlend = 0f;
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;

            _walkSource = gameObject.AddComponent<AudioSource>();
            _walkSource.spatialBlend = 0f;
            _walkSource.loop = true;
            _walkSource.playOnAwake = false;

            if (backgroundMusic != null)
            {
                _musicSource.clip = backgroundMusic;
                _musicSource.Play();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Play(SoundClip sound, float categoryVolume)
        {
            if (sound == null || sound.clip == null) return;
            if (_sfxSource == null) return;
            float vol = sound.volume * categoryVolume * sfxVolume;
            if (vol <= 0f) return;
            _sfxSource.PlayOneShot(sound.clip, vol);
        }

        public static void PlayIngredientChopped(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.ingredientChopped, _instance.cookingVolume);
        }

        public static void PlaySlotProcessOn(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.slotProcessOn, _instance.cookingVolume);
        }

        public static void PlaySlotProcessOff(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.slotProcessOff, _instance.cookingVolume);
        }

        // Pre: slotKey uniquely identifies the slot (typically GetInstanceID()).
        // Post: a looping AudioSource keyed by slotKey is playing the sauté or boil
        // clip selected by category; repeat calls reuse the existing source.
        public static void StartCookingLoop(int slotKey, ProcessCategory category)
        {
            if (_instance == null) return;
            SoundClip clip = category == ProcessCategory.WetHeat
                ? _instance.boilLoop
                : _instance.sauteeLoop;
            if (clip == null || clip.clip == null) return;

            float vol = clip.volume * _instance.cookingVolume * _instance.sfxVolume;
            if (vol <= 0f) return;

            if (!_instance._activeLoops.TryGetValue(slotKey, out var source) || source == null)
            {
                source = _instance.gameObject.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.loop = true;
                source.playOnAwake = false;
                _instance._activeLoops[slotKey] = source;
            }

            if (source.clip != clip.clip) source.clip = clip.clip;
            source.volume = vol;
            if (!source.isPlaying) source.Play();
        }

        public static void StopCookingLoop(int slotKey)
        {
            if (_instance == null) return;
            if (!_instance._activeLoops.TryGetValue(slotKey, out var source)) return;

            if (source != null)
            {
                source.Stop();
                Destroy(source);
            }
            _instance._activeLoops.Remove(slotKey);
        }

        public static void PlayCreatureFed(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.creatureFed, _instance.creatureVolume);
        }

        public static void PlayCreatureReaction(EmotionType emotion, Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.ClipForEmotion(emotion), _instance.creatureVolume);
        }

        private SoundClip ClipForEmotion(EmotionType emotion)
        {
            switch (emotion)
            {
                case EmotionType.Cozy:         return creatureCozy;
                case EmotionType.Refreshed:    return creatureRefreshed;
                case EmotionType.Spicy:        return creatureSpicy;
                case EmotionType.Confused:     return creatureConfused;
                case EmotionType.Disappointed: return creatureDisappointed;
                case EmotionType.Disgusted:    return creatureDisgusted;
                case EmotionType.Delighted:    return creatureDelighted;
                default:                       return null;
            }
        }

        // Pre: called from PlayerController when movement input becomes non-zero.
        // Post: the walk loop is playing on _walkSource; repeat calls are idempotent.
        public static void StartWalkLoop()
        {
            if (_instance == null) return;
            var src = _instance._walkSource;
            var clip = _instance.walkLoop;
            if (src == null || clip == null || clip.clip == null) return;

            float vol = clip.volume * _instance.playerVolume * _instance.sfxVolume;
            if (vol <= 0f) return;

            if (src.clip != clip.clip) src.clip = clip.clip;
            src.volume = vol;
            if (!src.isPlaying) src.Play();
        }

        public static void StopWalkLoop()
        {
            if (_instance == null) return;
            if (_instance._walkSource != null && _instance._walkSource.isPlaying)
                _instance._walkSource.Stop();
        }

        public static void PlayIngredientSpawned(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.ingredientSpawned, _instance.ingredientVolume);
        }

        public static void PlayIngredientPickedUp(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.ingredientPickedUp, _instance.ingredientVolume);
        }

        public static void PlayIngredientDropped(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.ingredientDropped, _instance.ingredientVolume);
        }

        public static void PlayPlateSpawned(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.plateSpawned, _instance.platingVolume);
        }

        public static void PlayItemPlacedOnPlate(Vector3 pos)
        {
            if (_instance == null) return;
            _instance.Play(_instance.itemPlacedOnPlate, _instance.platingVolume);
        }
    }
}
