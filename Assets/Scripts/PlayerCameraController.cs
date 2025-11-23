using UnityEngine;
using Photon.Pun;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;            // Genelde Player (root)

    [Header("Follow")]
    public float followSpeed = 10f;
    public float rotationLerpSpeed = 20f;

    [Header("FPS Offset")]
    public Vector3 fpsLocalOffset = new Vector3(0f, 1.7f, 0.1f); // player local space offset

    [Header("Look Settings")]
    public float mouseSensitivityY = 3f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private PhotonView pv;
    private float pitch = 0f;   // kamera dikey açýsý

    private void Awake()
    {
        pv = GetComponentInParent<PhotonView>();

        // Hedef atanmadýysa parent Player root'u kullan
        if (pv != null && target == null)
            target = pv.transform;
    }

    private void Start()
    {
        // Sadece local player için kamera aktif olsun
        if (pv != null && !pv.IsMine)
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.enabled = false;

            var listener = GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }

        // Mouse'u kilitlemek istersen (istersen kapatabilirsin)
        if (pv != null && pv.IsMine)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        // Sadece local oyuncunun kamerasý çalýþsýn
        if (pv == null || !pv.IsMine)
            return;

        if (target == null)
            return;

        HandleLook();
        HandleFollow();
    }

    private void HandleLook()
    {
        // Mouse Y ile pitch (dikey) kontrol
        float mouseY = Input.GetAxis("Mouse Y");
        pitch -= mouseY * mouseSensitivityY;      // ters çevirme (FPS oyunlardaki klasik)
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleFollow()
    {
        // Konum: player pozisyonu + local offset
        Vector3 localOffsetWorld = target.TransformDirection(fpsLocalOffset);
        Vector3 desiredPos = target.position + localOffsetWorld;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            followSpeed * Time.deltaTime
        );

        // Rotasyon: Yaw player'dan gelir, pitch kamerada
        Quaternion yawRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);
        Quaternion desiredRot = yawRot * pitchRot;

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            desiredRot,
            rotationLerpSpeed * Time.deltaTime
        );
    }
}
