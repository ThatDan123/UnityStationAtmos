using UnityEditor;
using UnityEngine;

namespace ECSAtmos.Editor
{
    public class AtmosCheckEditor : EditorWindow
    {
        private SceneView currentSceneView;

        private int tab = 0;

        private string[] tabHeaders = { "Entity", "Atmos" };
        private BasicView[] tabs = {new EntityView(), new MetaDataView()};



        [MenuItem("Window/Matrix Check")]
        public static void ShowWindow()
        {
            GetWindow<AtmosCheckEditor>("Atmos Check");
        }

        public void OnEnable()
        {
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui  += OnSceneGUI;
#else
	SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
        }

        public void OnDisable()
        {
#if UNITY_2019_3_OR_NEWER
            SceneView.duringSceneGui  += OnSceneGUI;
#else
	SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            currentSceneView = sceneView;
        }

        private void OnGUI()
        {
            tab = GUILayout.Toolbar(tab, tabHeaders);

            tabs[tab].OnGUI();

            if (currentSceneView)
            {
                currentSceneView.Repaint();
            }
        }
    }
    
    public abstract class BasicView
    {
        private Vector2 scrollPosition = Vector2.zero;
	
        public void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
		
            DrawContent();
		
            GUILayout.EndScrollView();
        }

        public abstract void DrawContent();
    }
    
    public abstract class Check
    {
        public bool Active { get; set; }
        public abstract string Label { get; }

        public virtual void DrawGizmo(BoundsInt bounds)
        {
        }

        public virtual void DrawLabel(BoundsInt bounds)
        {
        }
    }
}


