using UnityEngine;
using UnityEngine.Serialization;

namespace Galatea.Data
{
    // One-shot cut process. On completion the slot writes ResultForm to the
    // ingredient (CookState is untouched — chopping doesn't cook).
    [CreateAssetMenu(fileName = "NewDiscreteVariant", menuName = "Galatea/Discrete Variant")]
    public class DiscreteVariant : ProcessVariant
    {
        [FormerlySerializedAs("resultTextureState")]
        [SerializeField] private Form resultForm = Form.Chopped;

        public Form ResultForm => resultForm;
    }
}
