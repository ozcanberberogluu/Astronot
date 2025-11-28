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
    private float pitch = 0f;

    // Screen shake
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private float shakeDuration = 0f;
    private float shakeAmplitude = 0f;

    private void Awake()
    {
        pv = GetComponentInParent<PhotonView>();

        if (pv != null && target == null)
            target = pv.transform;
    }

    private void Start()
    {
        // Local olmayan oyuncularýn kamerasýný kapatalým
        if (pv != null && !pv.IsMine)
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.enabled = false;

            var listener = GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;

            enabled = false; // Uzaktan kameralar LateUpdate çalýþtýrmasýn
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleLook();
        HandleFollow();
        ApplyShake();
    }

    private void HandleLook()
    {
        float mouseY = Input.GetAxis("Mouse Y");
        pitch -= mouseY * mouseSensitivityY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleFollow()
    {
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

    private void ApplyShake()
    {
        if (!isShaking) return;

        shakeTimer += Time.deltaTime;
        if (shakeTimer >= shakeDuration)
        {
            isShaking = false;
            return;
        }

        // Zamanla azalan þiddet
        float t = 1f - (shakeTimer / shakeDuration);
        float currentAmp = shakeAmplitude * t;

        Vector3 offset = Random.insideUnitSphere * currentAmp;
        offset.z = 0f; // ileri/geri çok atmasýn, sadece X/Y sallansýn

        transform.position += offset;
    }

    /// <summary>
    /// Yaratýk adýmlarýnda vs. çaðýrmak için kamera sarsma fonksiyonu.
    /// </summary>
    public void ShakeCamera(float amplitude, float duration)
    {
        if (pv != null && !pv.IsMine) return; // sadece kendi kameramýzý salla

        shakeAmplitude = amplitude;
        shakeDuration = duration;
        shakeTimer = 0f;
        isShaking = true;
    }
}
