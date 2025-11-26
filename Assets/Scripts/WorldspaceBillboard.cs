using UnityEngine;

public class WorldspaceBillboard : MonoBehaviour
{
    private Camera cam;

    [Tooltip("Yazý hala ters görünüyorsa bunu iþaretle.")]
    public bool invertForward = true;

    [Tooltip("Ýstersen ekstra rotasyon offset'i verebilirsin.")]
    public Vector3 rotationOffset = Vector3.zero;

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // Canvas'ýn kameraya bakmasý için
        Vector3 dir = cam.transform.position - transform.position; // kamera -> canvas yönü
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        if (invertForward)
        {
            // UI'ler çoðunlukla -Z'e baktýðý için 180 derece döndürüyoruz
            rot *= Quaternion.Euler(0f, 180f, 0f);
        }

        if (rotationOffset != Vector3.zero)
        {
            rot *= Quaternion.Euler(rotationOffset);
        }

        transform.rotation = rot;
    }
}
