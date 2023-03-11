using ScientificGameJam.SO;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScientificGameJam.Player
{
    public class PlayerController : MonoBehaviour
    {
        public PlayerInfo Info { set; private get; }

        private Rigidbody2D _rb;
        private PlayerInput _input;
        private Camera _cam;
        private LineRenderer _lr;

        private float _laserTimer;

        // Movement vector
        private Vector2 _mov;
        // Movement vector on previous physic frame
        private Vector2 _prevMov;

        // Internal timer to calculate boost
        private float _boostTimer;

        private Vector2 _aimDir;

        public ColorType Color => Info.Color;

        public int _ignoreMask;

        private float _shake;

        private float _decreaseFactor=0.7f;

        private void Shake()
        {
            if (_shake > 0f)
            {
                _cam.transform.localPosition = UnityEngine.Random.insideUnitCircle * Info.ShakeAmount;
                _cam.transform.localPosition = new(_cam.transform.localPosition.x, _cam.transform.localPosition.y, -10f);
                _shake -= Time.deltaTime * _decreaseFactor;

            }
            else
            {
                _shake = 0f;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _input = GetComponent<PlayerInput>();
            _cam = GetComponentInChildren<Camera>();
            _lr = GetComponentInChildren<LineRenderer>();
            _lr.gameObject.SetActive(false);

            _ignoreMask = (1 << LayerMask.NameToLayer("Player"));
            _ignoreMask |= (1 << LayerMask.NameToLayer("Collectible"));
            _ignoreMask = ~_ignoreMask;
        }

        private void FixedUpdate()
        {
            if (PlayerManager.Instance.IsReady)

            {
                // Debug.Log($"Dot product value {Vector2.Dot(_prevMov, _mov)}");
                if (Vector2.Dot(_prevMov, _mov) < Info.DeviationLimit) // condition on loosing booster
                {
                    //Debug.Log("I did a reset");
                    _boostTimer = 0f; // in seconds
                }
                

                _prevMov = _mov;
                _rb.velocity = Info.Speed * Time.fixedDeltaTime * _mov * (_boostTimer >= Info.TimeBeforeBoost ? Info.Booster * ( 1f+ Info.BoostCurve.Evaluate(Time.fixedDeltaTime)) : 1f);
                // Debug.Log($"Velocity {_rb.velocity.magnitude}");
            }
        }

        private void Update()
        {
            _boostTimer += Time.deltaTime;
            if (_laserTimer > 0f)
            {
                _laserTimer -= Time.deltaTime;
                if (_laserTimer <= 0f)
                {
                    _lr.gameObject.SetActive(false);
                }
            }
            Shake();
        }

        public void Move(InputAction.CallbackContext value)
        {
            _mov = value.ReadValue<Vector2>().normalized;
        }

        public void OnTeleport(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                var next = PlayerManager.Instance.GetNextPlayer(_input);
                if (next != null) // Might happens if the others players aren't instanciated yet
                {
                    (next.transform.position, transform.position) = (transform.position, next.transform.position);
                }
            }
        }

        public void OnAim(InputAction.CallbackContext value)
        {
            var v2 = value.ReadValue<Vector2>();
            if (_input.currentControlScheme == "Keyboard&Mouse")
            {
                v2 = _cam.ScreenToWorldPoint(v2);
                _aimDir = new(v2.x - transform.position.x, v2.y - transform.position.y);
            }
            else
            {
                if (v2 != Vector2.zero)
                {
                    _aimDir = new(v2.x, v2.y);
                    Debug.Log(_aimDir);
                }
            }
        }

        public void OnFire(InputAction.CallbackContext value)
        {
            if (value.performed && Info.CanShoot)
            {
                var hit = Physics2D.Raycast(transform.position, _aimDir, float.PositiveInfinity, _ignoreMask);
                if (hit.collider != null)
                {
                    _shake = Info.ShakeTime;
                    _lr.gameObject.SetActive(true);
                    _lr.SetPositions(new[] { transform.position, (Vector3)hit.point });
                    _laserTimer = .3f;
                    if (hit.collider.TryGetComponent<Rigidbody2D>(out var comp))
                    {
                        comp.AddForce(_aimDir.normalized * 10f, ForceMode2D.Impulse);
                    }
                }
            }
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = UnityEngine.Color.blue;
            Gizmos.DrawRay(new(transform.position, _aimDir));
        }
    }
}
