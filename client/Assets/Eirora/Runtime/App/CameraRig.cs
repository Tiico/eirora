using System;
using Eirora.Client.Look;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Eirora.Client.App
{
    /// <summary>
    /// The tilted game camera (art-direction.md, section 2): drag to pan, pinch or scroll to zoom between three
    /// levels, tap to select a place (UX-01).
    /// </summary>
    public sealed class CameraRig
    {
        private const float Pitch = 50f;
        private const float TapMovement = 14f;
        private const float TapSeconds = 0.45f;
        private const float PanLimit = 8f;
        private static readonly float[] s_distances = { 18f, 28f, 44f };

        private readonly Camera _camera;
        private readonly Func<Vector2, bool> _overInterface;
        private Vector3 _pivot = new(0.3f, Models.LandHeight, -1.6f);
        private float _distance = s_distances[1];
        private Vector2 _pressPosition;
        private float _pressTime;
        private bool _pressed;
        private bool _dragging;
        private bool _pressOverInterface;
        private Vector3 _dragAnchor;
        private float? _pinchStart;
        private float _pinchDistance;

        public CameraRig(Camera camera, Func<Vector2, bool> overInterface)
        {
            _camera = camera;
            _overInterface = overInterface;
            _camera.fieldOfView = 36f;
            _camera.nearClipPlane = 0.5f;
            _camera.farClipPlane = 160f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            Place();
        }

        /// <summary>Raised when the player taps the island; the point is on the ground plane.</summary>
        public event Action<Vector3>? Tapped;

        public void Update()
        {
            if (Pinch())
            {
                return;
            }

            if (Mouse.current is { } mouse && mouse.scroll.ReadValue().y is float scroll && scroll != 0f && !_overInterface(mouse.position.ReadValue()))
            {
                Zoom(scroll > 0f ? -1 : 1);
            }

            var pointer = Pointer.current;
            if (pointer is null)
            {
                return;
            }

            var position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                _pressed = true;
                _dragging = false;
                _pressPosition = position;
                _pressTime = Time.unscaledTime;
                _pressOverInterface = _overInterface(position);
                _dragAnchor = Ground(position);
            }
            else if (_pressed && pointer.press.isPressed && !_pressOverInterface)
            {
                if (!_dragging && (position - _pressPosition).magnitude > TapMovement)
                {
                    _dragging = true;
                }

                if (_dragging)
                {
                    var delta = _dragAnchor - Ground(position);
                    _pivot += new Vector3(delta.x, 0f, delta.z);
                    _pivot.x = Mathf.Clamp(_pivot.x, -PanLimit, PanLimit);
                    _pivot.z = Mathf.Clamp(_pivot.z, -PanLimit, PanLimit);
                    Place();
                }
            }
            else if (_pressed && pointer.press.wasReleasedThisFrame)
            {
                _pressed = false;
                if (!_dragging && !_pressOverInterface && Time.unscaledTime - _pressTime < TapSeconds)
                {
                    Tapped?.Invoke(Ground(position));
                }
            }
        }

        private bool Pinch()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen is null || touchscreen.touches.Count < 2 || !touchscreen.touches[1].press.isPressed)
            {
                _pinchStart = null;
                return false;
            }

            _pressed = false;
            float apart = (touchscreen.touches[0].position.ReadValue() - touchscreen.touches[1].position.ReadValue()).magnitude;
            if (_pinchStart is null)
            {
                _pinchStart = apart;
                _pinchDistance = _distance;
            }

            _distance = Mathf.Clamp(_pinchDistance * (_pinchStart.Value / Mathf.Max(apart, 1f)), s_distances[0], s_distances[^1]);
            Place();
            return true;
        }

        private void Zoom(int step)
        {
            int nearest = 0;
            for (int i = 1; i < s_distances.Length; i++)
            {
                if (Mathf.Abs(s_distances[i] - _distance) < Mathf.Abs(s_distances[nearest] - _distance))
                {
                    nearest = i;
                }
            }

            _distance = s_distances[Mathf.Clamp(nearest + step, 0, s_distances.Length - 1)];
            Place();
        }

        private void Place()
        {
            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            _camera.transform.SetPositionAndRotation(_pivot - (rotation * Vector3.forward * _distance), rotation);
        }

        private Vector3 Ground(Vector2 screenPosition)
        {
            var ray = _camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, Models.LandHeight, 0f));
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : _pivot;
        }
    }
}
