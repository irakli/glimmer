using UnityEngine;
using UnityEngine.UI;

namespace IrakliChkuaseli.UI.Glimmer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Effects/Glimmer Element")]
    public class GlimmerElement : MonoBehaviour
    {
        [SerializeField] private bool ignoreGlimmer;
        [SerializeField] private bool overrideCornerRadius;
        [SerializeField] private float cornerRadius;

        public bool IgnoreGlimmer => ignoreGlimmer;
        public bool HasCornerRadiusOverride => overrideCornerRadius;
        public float CornerRadius => cornerRadius;
    }
}
