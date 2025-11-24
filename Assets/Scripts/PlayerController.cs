using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviourPun
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float slowWalkSpeed = 2f;
    public float mouseSensitivity = 3f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private CharacterController controller;
    private Animator animator;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsLowHPHash = Animator.StringToHash("IsLowHP");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        // Radar için RadarTarget component'ini otomatik ekle
        RadarTarget radarTarget = GetComponent<RadarTarget>();
        if (radarTarget == null)
        {
            radarTarget = gameObject.AddComponent<RadarTarget>();
            radarTarget.type = RadarTargetType.Player;
        }
    }

    private void Start()
    {
        // Sadece kendi player'�m�zda kamera / ses a��k olsun
        if (!photonView.IsMine)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = false;

            var listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        HandleMovement();
        UpdateAnimator();
    }

    private void HandleMovement()
    {
        if (controller == null) return;

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Y d�zleminde input
        Vector3 inputDir = new Vector3(inputX, 0f, inputZ);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        // Karakter y�n�ne g�re d�nya uzay�na �evir
        Vector3 moveDirWorld = transform.TransformDirection(inputDir);

        float healthPercent = currentHealth / maxHealth;
        bool isLowHP = healthPercent < 0.3f;

        bool wantsRun = Input.GetKey(KeyCode.LeftShift);
        bool wantsSlow = Input.GetKey(KeyCode.LeftControl);

        float speed = walkSpeed;

        // LowHP iken Shift / Ctrl devre d���
        if (!isLowHP)
        {
            if (wantsRun)
                speed = runSpeed;
            else if (wantsSlow)
                speed = slowWalkSpeed;
        }

        // SimpleMove: hem hareket hem yer�ekimi
        controller.SimpleMove(moveDirWorld * speed);

        // Mouse X ile karakteri d�nd�r
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseX * mouseSensitivity);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float healthPercent = currentHealth / maxHealth;
        bool isLowHP = healthPercent < 0.3f;
        animator.SetBool(IsLowHPHash, isLowHP);

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        float moveMag = Mathf.Clamp01(new Vector2(inputX, inputZ).magnitude);

        bool wantsRun = Input.GetKey(KeyCode.LeftShift);
        bool wantsSlow = Input.GetKey(KeyCode.LeftControl);

        float animSpeed = 0f;

        if (moveMag > 0.01f)
        {
            animSpeed = 0.7f; // normal y�r�y��

            if (!isLowHP)
            {
                if (wantsRun)
                    animSpeed = 1f;    // Run
                else if (wantsSlow)
                    animSpeed = 0.5f;  // SlowWalk
            }
            else
            {
                animSpeed = 0.4f;      // Low HP'de yava�lama
            }
        }

        // MoveSpeed blend tree i�in
        animator.SetFloat(MoveSpeedHash, animSpeed, 0.1f, Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        if (!photonView.IsMine) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        // �l�m / respawn sonradan eklenecek
    }
}
