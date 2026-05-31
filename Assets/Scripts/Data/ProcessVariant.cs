using UnityEngine;

namespace Galatea.Data
{
    public enum ProcessCategory { Cut, DryHeat, WetHeat, Extraction }

    // Base for cooking/cut process definitions. Concrete subclasses own their
    // result-state field directly because cut (Form) and heat (CookState) use
    // different enums.
    public abstract class ProcessVariant : ScriptableObject
    {
        [SerializeField] private string variantName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ProcessCategory category;

        public string VariantName => variantName;
        public string Description => description;
        public ProcessCategory Category => category;
    }
}
