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
    public float gravity = -3f; // Düşük yer çekimi - astronot hissiyatı için
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer = 1; // Default layer

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

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
            // Diğer oyuncular için sadece ground check
            CheckGrounded();
            return;
        }

        HandleMovement();
        UpdateAnimator();
    }

    private void CheckGrounded()
    {
        if (controller == null) return;

        // CharacterController'ın isGrounded'ı bazen gecikmeli olabilir, ekstra kontrol
        isGrounded = controller.isGrounded;

        // Ekstra ground check - daha hassas kontrol için
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

        // Hareketten önce ground check yap (zıplama kontrolü için)
        CheckGrounded();

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Y düzleminde input
        Vector3 inputDir = new Vector3(inputX, 0f, inputZ);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        // Karakter yönüne göre dünya uzayına çevir
        Vector3 moveDirWorld = transform.TransformDirection(inputDir);

        float healthPercent = currentHealth / maxHealth;
        bool isLowHP = healthPercent < 0.3f;

        bool wantsRun = Input.GetKey(KeyCode.LeftShift);
        bool wantsSlow = Input.GetKey(KeyCode.LeftControl);

        float speed = walkSpeed;

        // LowHP iken Shift / Ctrl devre dışı
        if (!isLowHP)
        {
            if (wantsRun)
                speed = runSpeed;
            else if (wantsSlow)
                speed = slowWalkSpeed;
        }

        // Zıplama kontrolü
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalVelocity = jumpForce;
            
            // Animator'a jump trigger gönder
            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }

            // Multiplayer senkronizasyonu için RPC gönder
            photonView.RPC("OnJump", RpcTarget.Others);
        }

        // Yer çekimi uygula - her zaman uygula (havadayken bile)
        // Bu sayede karakter havada kalmaz, sürekli düşer
        verticalVelocity += gravity * Time.deltaTime;

        // Hareket vektörü: yatay hareket + dikey (jump/gravity)
        Vector3 moveVector = moveDirWorld * speed + Vector3.up * verticalVelocity;

        // CharacterController.Move ile hareket et
        controller.Move(moveVector * Time.deltaTime);

        // Hareket sonrası ground check yap (vertical velocity sıfırlama için)
        CheckGrounded();

        // Yerdeyse ve düşüyorsa vertical velocity'yi sıfırla
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -0.5f; // Küçük negatif değer yerde kalmayı sağlar
        }

        // Mouse X ile karakteri döndür
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseX * mouseSensitivity);
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
            animSpeed = 0.7f; // normal yürüyüş

            if (!isLowHP)
            {
                if (wantsRun)
                    animSpeed = 1f;    // Run
                else if (wantsSlow)
                    animSpeed = 0.5f;  // SlowWalk
            }
            else
            {
                animSpeed = 0.4f;      // Low HP'de yavaşlama
            }
        }

        // MoveSpeed blend tree için
        animator.SetFloat(MoveSpeedHash, animSpeed, 0.1f, Time.deltaTime);
    }

    [PunRPC]
    private void OnJump()
    {
        // Diğer oyuncuların zıplama animasyonunu tetikle
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
