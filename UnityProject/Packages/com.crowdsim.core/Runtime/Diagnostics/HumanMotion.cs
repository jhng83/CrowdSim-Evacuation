using System;
using System.Numerics;

namespace CrowdSim.Diagnostics
{
    /// <summary>Render-only interpolation and gait clock. Never extrapolates or writes simulation values.</summary>
    public sealed class HumanMotion
    {
        private int _tick = -1;
        private Vector2 _previous, _current;
        private float _elapsed;
        private float _previousRise, _currentRise;
        private readonly float _initialPhase;
        public Vector2 Position { get; private set; }
        public float Phase { get; private set; }
        public float Rise { get; private set; }
        public HumanMotion(int id) { _initialPhase = (id * 2.39996323f) % (MathF.PI * 2); Phase = _initialPhase; }
        public void Observe(int tick, Vector2 position, float rise = 0)
        {
            rise = Math.Clamp(rise, 0, 1);
            if (_tick < 0 || tick < _tick)
            {
                _previous = position; _current = position; Position = position;
                _previousRise = rise; Rise = rise; _elapsed = 0; Phase = _initialPhase;
            }
            else if (tick != _tick) { _previous = Position; _current = position; _previousRise = Rise; _elapsed = 0; }
            _currentRise = rise;
            _tick = tick;
        }
        public void Advance(float elapsed, bool running, float speed, float stepSeconds, float speedRatio)
        {
            if (!running) { Position = _current; Rise = _currentRise; return; }
            float delta = Math.Clamp(elapsed, 0, .1f) * Math.Clamp(speed, .25f, 4);
            _elapsed += delta;
            float blend = Math.Clamp(_elapsed / Math.Max(.001f, stepSeconds), 0, 1);
            Position = Vector2.Lerp(_previous, _current, blend);
            Rise = _previousRise + (_currentRise - _previousRise) * blend;
            Phase = (Phase + MathF.PI * 2 * HumanGeometry.Cadence(speedRatio) * delta) % (MathF.PI * 2);
        }
    }
}
