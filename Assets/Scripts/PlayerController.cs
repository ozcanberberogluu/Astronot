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

    [Header("Jump & Gravity")]
    public float jumpForce = 8f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer = 1; // Default layer

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Storm Effect")]
    [Tooltip("Fırtına sırasında rüzgarın yatay kuvvet çarpanı.")]
    public float stormWindMultiplier = 0.5f;

    private CharacterController controller;
    private Animator animator;

    private float verticalVelocity = 0f;
    private bool isGrounded = false;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsLowHPHash = Animator.StringToHash("IsLowHP");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        // RadarTarget yoksa ekle
        RadarTarget radarTarget = GetComponent<RadarTarget>();
        if (radarTarget == null)
        {
            radarTarget = gameObject.AddComponent<RadarTarget>();
            radarTarget.type = RadarTargetType.Player;
        }
    }

    private void Start()
    {
        // Sadece kendi player'ımızda kamera / ses açık olsun
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
        {
            CheckGrounded();
            return;
        }

        HandleMovement();
        UpdateAnimator();
    }

    private void CheckGrounded()
    {
        if (controller == null) return;

        isGrounded = controller.isGrounded;

        if (!isGrounded)
        {
            Vector3 rayStart = transform.position + controller.center;
            float rayDistance = controller.height * 0.5f + groundCheckDistance;
            isGrounded = Physics.Raycast(rayStart, Vector3.down, rayDistance, groundLayer);
        }
    }

    private void HandleMovement()
    {
        if (controller == null) return;

        bool stormActive = StormManager.Instance != null && StormManager.Instance.IsStormActive;

        // Önce zemin kontrolü
        CheckGrounded();

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(inputX, 0f, inputZ);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        Vector3 moveDirWorld = transform.TransformDirection(inputDir);

        float healthPercent = currentHealth / maxHealth;
        bool isLowHP = healthPercent < 0.3f;

        bool wantsRun = Input.GetKey(KeyCode.LeftShift);
        bool wantsSlow = Input.GetKey(KeyCode.LeftControl);

        float speed = walkSpeed;

        if (!isLowHP)
        {
            if (wantsRun)
                speed = runSpeed;
            else if (wantsSlow)
                speed = slowWalkSpeed;
        }

        // Zıplama
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalVelocity = jumpForce;

            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }

            photonView.RPC("OnJump", RpcTarget.Others);
        }

        // Normal gravity (fırtınada da aynısı)
        verticalVelocity += gravity * Time.deltaTime;

        // Ana hareket vektörü
        Vector3 moveVector = moveDirWorld * speed + Vector3.up * verticalVelocity;

        // Fırtına varsa sadece YATAY rüzgar ekle (yukarı kaldırma yok)
        if (stormActive && StormManager.Instance != null)
        {
            Vector3 wind = StormManager.Instance.CurrentWindHorizontal * stormWindMultiplier;
            moveVector += wind;
        }

        controller.Move(moveVector * Time.deltaTime);

        // Hareket sonrası zemin kontrolü
        CheckGrounded();

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        // Mouse X ile karakteri döndür
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseSensitivity * mouseX);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float healthPercent = currentHealth / maxHealth;
        bool isLowHP = healthPercent < 0.3f;
        animator.SetBool(IsLowHPHash, isLowHP);
        animator.SetBool(IsGroundedHash, isGrounded);

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        float moveMag = Mathf.Clamp01(new Vector2(inputX, inputZ).magnitude);

        bool wantsRun = Input.GetKey(KeyCode.LeftShift);
        bool wantsSlow = Input.GetKey(KeyCode.LeftControl);

        float animSpeed = 0f;

        if (moveMag > 0.01f)
        {
            animSpeed = 0.7f;

            if (!isLowHP)
            {
                if (wantsRun)
                    animSpeed = 1f;
                else if (wantsSlow)
                    animSpeed = 0.5f;
            }
            else
            {
                animSpeed = 0.4f;
            }
        }

        animator.SetFloat(MoveSpeedHash, animSpeed, 0.1f, Time.deltaTime);
    }

    [PunRPC]
    private void OnJump()
    {
        if (animator != null)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    public void TakeDamage(float amount)
    {
        if (!photonView.IsMine) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        // Ölüm / respawn sonradan eklenecek
    }
}
