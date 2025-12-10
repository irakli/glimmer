using UnityEditor;

namespace IrakliChkuaseli.UI.Glimmer.Editor
{
    [InitializeOnLoad]
    internal static class GlimmerPlayModeHandler
    {
        static GlimmerPlayModeHandler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            foreach (var glimmer in GlimmerGroup.ActiveInstances)
            {
                if (glimmer != null && glimmer.IsShowing)
                    glimmer.Hide();
            }
        }
    }
}
