using UnityEngine;

namespace Galatea.Data
{
    public enum EmotionType
    {
        Cozy,
        Refreshed,
        Spicy,
        Delighted,
        Confused,
        Disappointed,
        Disgusted
    }

    [CreateAssetMenu(fileName = "NewEmotionalResponse", menuName = "Galatea/Emotional Response")]
    public class EmotionalResponse : ScriptableObject
    {
        [SerializeField] private EmotionType emotion;
        [SerializeField] private Gradient colorGradient;
        [SerializeField, TextArea] private string reactionLine;

        public EmotionType Emotion => emotion;
        public Gradient ColorGradient => colorGradient;
        public string ReactionLine => reactionLine;
    }
}
