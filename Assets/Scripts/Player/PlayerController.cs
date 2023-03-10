using ScientificGameJam.SFX;
using ScientificGameJam.SO;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ScientificGameJam.Player
{
    public class PlayerController : MonoBehaviour
    {
        public PlayerInfo Info { set; private get; }

        [SerializeField]
        private TMP_Text _dyeLeftText;

        [SerializeField]
        private Image _dyeLeftImage;

        [SerializeField]
        private GameObject _explosion;

        private Rigidbody2D _rb;
        private PlayerInput _input;
        private Camera _cam;
        private LineRenderer _lr;
        private SpriteRenderer _sr;

        private float _laserTimer;

        private bool _canShoot = true;

        // Movement vector
        private Vector2 _mov;
        // Movement vector on previous physic frame
        private Vector2 _prevMov;

        // Internal timer to calculate boost
        private float _boostTimer;

        private Vector2 _aimDir;

        public ColorType Color => Info.Color;

        private int _ignoreMask;

        private float _shake;

        private float _decreaseFactor=0.7f;

        private float _stunDuration;

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
            _sr = GetComponentInChildren<SpriteRenderer>();
            _lr.gameObject.SetActive(false);

            UpdateDyeText();
        }

        private void Start()
        {
            if (_dyeLeftImage != null)
                _dyeLeftImage.color = PlayerManager.ToColor(Info.Color);

            _ignoreMask = (1 << gameObject.layer);
            _ignoreMask |= (1 << LayerMask.NameToLayer("Collectible"));
            _ignoreMask = ~_ignoreMask;
        }

        private bool CanPlay => PlayerManager.Instance.IsReady && !PlayerManager.Instance.DidGameEnded;

        private void FixedUpdate()
        {
            if (!CanPlay)
            {
                _rb.velocity = Vector2.zero;
            }
            else if (_stunDuration <= 0f)
            {
                if (Vector2.Dot(_prevMov, _mov) < Info.DeviationLimit) // condition on loosing booster
                {
                    _boostTimer = 0f; // in seconds
                }
                

                _prevMov = _mov;
                _rb.velocity = Info.Speed * Time.fixedDeltaTime * _mov * (_boostTimer >= Info.TimeBeforeBoost ? Info.Booster * ( 1f+ Info.BoostCurve.Evaluate(Time.fixedDeltaTime)) : 1f);

                if (_rb.velocity.x < 0f)
                {
                    _sr.flipX = true;
                }
                else if (_rb.velocity.x > 0f)
                {
                    _sr.flipX = false;
                }
            }
        }

        private void Update()
        {
            _boostTimer += Time.deltaTime;
            _stunDuration -= Time.deltaTime;
            if (_laserTimer > 0f)
            {
                _laserTimer -= Time.deltaTime;
                if (_laserTimer <= 0f)
                {
                    _lr.gameObject.SetActive(false);
                }
            }
            Shake();
            _sr.sortingOrder = -Mathf.RoundToInt(transform.position.y * 1000f);
        }

        public void Stun()
        {
            _stunDuration = 1f;
        }

        public void UpdateDyeText()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                _dyeLeftText.text = $"{PlayerManager.Instance.GetCollectibleLeft(Info.Color)}";
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.CompareTag("Player") && SceneManager.GetActiveScene().name != "MainMenu")
            {
                PlayerManager.Instance.GameOver(true);
            }
        }

        public void Move(InputAction.CallbackContext value)
        {
            _mov = value.ReadValue<Vector2>().normalized;
        }

        public void OnTeleport(InputAction.CallbackContext value)
        {
            if (value.performed && CanPlay)
            {
                var next = PlayerManager.Instance.GetNextPlayer(_input);
                if (next != null) // Might happens if the others players aren't instanciated yet
                {
                    (next.transform.position, transform.position) = (transform.position, next.transform.position);
                    if (SFXManager.Instance != null)
                        SFXManager.Instance.TeleportSFX.Play();
                }
            }
        }

        public void OnAim(InputAction.CallbackContext value)
        {
            var v2 = value.ReadValue<Vector2>();
            if (_input == null)
            {
                return;
            }
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
                }
            }
        }

        private Vector3 ConvertVector(Vector3 value)
            => new(value.x, value.y, -1f);

        public void OnFire(InputAction.CallbackContext value)
        {
            if (value.performed && Info.CanShoot && _canShoot && CanPlay)
            {
                var hit = Physics2D.Raycast(transform.position, _aimDir, float.PositiveInfinity, _ignoreMask);
                if (hit.collider != null)
                {
                    if (SFXManager.Instance != null)
                        SFXManager.Instance.LaserSFX.Play();
                    _shake = Info.ShakeTime;
                    _lr.gameObject.SetActive(true);
                    _lr.SetPositions(new[] { ConvertVector(transform.position), ConvertVector((Vector3)hit.point) });
                    _laserTimer = .3f;
                    if (hit.collider.CompareTag("Player"))
                    {
                        hit.collider.attachedRigidbody.GetComponent<PlayerController>().Stun();
                    }
                    var target = hit.collider.attachedRigidbody == null ? hit.collider.gameObject : hit.collider.attachedRigidbody.gameObject;
                    if (target.CompareTag("Destructible"))
                    {
                        Destroy(target.gameObject);
                        Destroy(Instantiate(_explosion, hit.point, Quaternion.identity), .7f);
                    }
                    else if (hit.collider.attachedRigidbody != null)
                    {
                        hit.collider.attachedRigidbody.AddForce(_aimDir.normalized * 20f, ForceMode2D.Impulse);
                    }
                    _canShoot = false;
                    StartCoroutine(Reload());
                }
            }
        }

        public void OnReset(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        private IEnumerator Reload()
        {
            yield return new WaitForSeconds(Info.LaserReloadTime);
            _canShoot = true;
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = UnityEngine.Color.blue;
            Gizmos.DrawRay(new(transform.position, _aimDir));
        }
    }
}
