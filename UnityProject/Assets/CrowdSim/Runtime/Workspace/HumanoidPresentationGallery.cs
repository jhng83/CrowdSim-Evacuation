using System.Collections.Generic;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using UnityEngine;
using Ground = System.Numerics.Vector2;
using ViewVector = System.Numerics.Vector3;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Deterministic visual fixtures only. No Simulation is created or physical state changed.</summary>
    public sealed class HumanoidPresentationGallery : MonoBehaviour
    {
        private WorkspaceWorldView _world;
        private readonly List<AgentVisualFrame> _samples = new List<AgentVisualFrame>();
        private readonly string[] _names = { "Standing / normal", "Walking / evacuating", "Running / evacuating", "Waiting / alarm",
            "Injured", "Crawling", "Fallen", "Temporary incapacity", "Permanent incapacity", "Fatal casualty",
            "Seated", "Standing up (cycled)", "Going to help #6", "Kneeling / helping #6" };
        private int _selected;
        private float _clock;
        private bool _running = true, _follow;
        private GUIStyle _text;
        private GUIStyle _button, _toggle;
        private void Start()
        {
            GameObject host = new GameObject("Pose reference world"); host.transform.SetParent(transform, false);
            _world = host.AddComponent<WorkspaceWorldView>(); _world.Initialise();
            _world.Draw(new[] { new ViewBody("floor", ViewShape.Box, ViewKind.Floor, new ViewVector(16, -.08f, 10), new ViewVector(32, .12f, 20)) });
        }
        private void Update()
        {
            if (_world == null) return;
            if (_running) _clock += Time.unscaledDeltaTime;
            _samples.Clear();
            for (int i = 0; i < _names.Length; i++)
            {
                AgentState state = i == 3 ? AgentState.WaitingToReact : i == 4 ? AgentState.Injured : i == 5 ? AgentState.Crawling
                    : i == 6 ? AgentState.Fallen : i == 7 ? AgentState.TempIncap : i == 8 ? AgentState.Incapacitated : i == 9 ? AgentState.Dead
                    : i == 0 || i == 10 ? AgentState.Wandering : AgentState.Evacuating;
                _samples.Add(new AgentVisualFrame(i, new Ground(5 + i % 4 * 7, 4 + i / 4 * 4), Ground.UnitX,
                    i == 2 ? 2.4f : i == 1 || i == 4 || i == 5 || i == 12 ? 1 : 0, state,
                    i == 10 ? SeatingPosture.Seated : i == 11 ? SeatingPosture.StandingUp : SeatingPosture.Walking,
                    i == 11 ? .5f + .5f * Mathf.Sin(_clock) : 0, i >= 1 && i <= 5 ? .7f : 0,
                    i >= 12 ? 6 : (int?)null, i == 13));
            }
            _world.Humans.RenderSamples(_samples, 0, Time.unscaledDeltaTime, _running, 1, .15f, _selected, AgentCameraMode.Overview);
            AgentCameraFrame frame = AgentCameraRig.Calculate(32, 20, Screen.width / Mathf.Max(1f, Screen.height * .7f),
                _world.Azimuth, _world.Elevation, _world.Zoom, _follow ? AgentCameraMode.Follow : AgentCameraMode.Overview, _samples[_selected]);
            Camera camera = _world.ViewCamera;
            camera.rect = new Rect(0, 0, 1, .7f); camera.orthographic = true; camera.orthographicSize = frame.Size;
            camera.transform.position = new Vector3(frame.Eye.X, frame.Eye.Y, frame.Eye.Z);
            camera.transform.LookAt(new Vector3(frame.Target.X, frame.Target.Y, frame.Target.Z));
        }
        private void OnGUI()
        {
            if (_world == null) return;
            if (_text == null)
            {
                Font font = Resources.Load<Font>("CrowdSim/Fonts/AtkinsonHyperlegible-Regular");
                _text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
                _button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 14 };
                _toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 14 };
                foreach (GUIStyleState state in new[] { _button.normal, _button.hover, _button.active, _button.focused, _button.onNormal, _button.onHover, _button.onActive, _button.onFocused })
                { state.textColor = Color.black; state.background = Texture2D.whiteTexture; }
                foreach (GUIStyleState state in new[] { _toggle.normal, _toggle.hover, _toggle.active, _toggle.focused, _toggle.onNormal, _toggle.onHover, _toggle.onActive, _toggle.onFocused })
                    state.textColor = Color.black;
            }
            Color saved = GUI.color; GUI.color = new Color(.965f, .96f, .935f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * .3f), Texture2D.whiteTexture); GUI.color = saved;
            _text.normal.textColor = Color.black;
            _button.normal.textColor = Color.black; _button.normal.background = Texture2D.whiteTexture;
            _toggle.normal.textColor = Color.black;
            GUI.Label(new Rect(20, 8, Screen.width - 40, 28), "M8 · Humanoid pose reference — presentation fixtures, no simulation", _text);
            GUI.Label(new Rect(20, 38, Screen.width - 40, 28), (_selected + 1) + " / " + _names.Length + " · " + _names[_selected], _text);
            if (GUI.Button(new Rect(20, 72, 100, 30), "Previous", _button)) _selected = (_selected + _names.Length - 1) % _names.Length;
            if (GUI.Button(new Rect(128, 72, 100, 30), "Next", _button)) _selected = (_selected + 1) % _names.Length;
            if (GUI.Button(new Rect(236, 72, 100, 30), _running ? "Pause" : "Animate", _button)) _running = !_running;
            _follow = GUI.Toggle(new Rect(350, 77, 160, 26), _follow, "Centre selected", _toggle);
            GUI.Label(new Rect(20, 111, 160, 25), "Zoom " + _world.Zoom.ToString("F2") + "×", _text);
            _world.Zoom = GUI.HorizontalSlider(new Rect(185, 119, 210, 22), _world.Zoom, .45f, 2.5f);
            GUI.Label(new Rect(415, 111, 90, 25), "Azimuth", _text);
            _world.Azimuth = GUI.HorizontalSlider(new Rect(510, 119, 170, 22), _world.Azimuth, -180, 180);
            GUI.Label(new Rect(20, 145, Screen.width - 40, 45), "Check all 14 fixtures at low/high zoom. Low detail still has six solid body parts. Pause freezes gait; the rising fixture cycles from seated to standing. Open Bootstrap for real hazards, selection, Follow and POV.", _text);
        }
    }
}
