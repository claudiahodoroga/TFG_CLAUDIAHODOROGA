using System;

namespace Galatea.Data
{
    // Runtime ingredient state. Form (cut shape) and CookState (heat progression)
    // are independent axes; each raises its own change event so listeners can
    // subscribe to just the channel they need.
    public class IngredientInstance
    {
        public IngredientData source;
        public FlavorProfile currentProfile;
        public float cookingProgress;

        private Form _form;
        public Form Form
        {
            get => _form;
            set
            {
                if (_form == value) return;
                _form = value;
                OnFormChanged?.Invoke(value);
            }
        }
        public event Action<Form> OnFormChanged;

        private CookState _cookState;
        public CookState CookState
        {
            get => _cookState;
            set
            {
                if (_cookState == value) return;
                _cookState = value;
                OnCookStateChanged?.Invoke(value);
            }
        }
        public event Action<CookState> OnCookStateChanged;

        public bool IsBurnt => _cookState == CookState.Burnt;

        public IngredientInstance(IngredientData source)
        {
            this.source = source;
            this.currentProfile = source.BaseProfile;
            this._form = Form.Whole;
            this._cookState = CookState.Raw;
            this.cookingProgress = 0f;
        }
    }
}
