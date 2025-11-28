using System.Collections;
using UnityEngine;
using Photon.Pun;

public class MeteorBehaviour : MonoBehaviourPun
{
    [Header("Movement")]
    [Tooltip("Meteorun düþme hýzý.")]
    public float moveSpeed = 40f;

    [Tooltip("Raycast için zemin layer'ý (Terrain, Ground vs.).")]
    public LayerMask groundMask = ~0;

    [Tooltip("Yer ile çarpýþma toleransý (çok küçük býrak).")]
    public float hitCheckDistance = 2f;

    [Header("Visual Roots")]
    [Tooltip("Meteor gövdesi (mesh vs.).")]
    public GameObject meteorRoot;

    [Tooltip("10 sn sonra aktif olacak elmas cevheri root'u.")]
    public GameObject diamondRoot;

    [Header("Impact FX")]
    public GameObject impactFxPrefab;
    public AudioClip impactAudioClip;
    public float impactVolume = 1f;
    public float impactMinDistance = 3f;
    public float impactMaxDistance = 50f;

    [Tooltip("Çarpma sonrasý kaç saniye sonra elmas aktif olacak?")]
    public float diamondRevealDelay = 10f;

    private bool hasImpacted = false;

    private void Awake()
    {
        if (meteorRoot != null) meteorRoot.SetActive(true);
        if (diamondRoot != null) diamondRoot.SetActive(false);
    }

    private void Update()
    {
        // Hareket ve çarpýþma kontrolü sadece master client'ta olsun
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        if (hasImpacted) return;

        MoveAndCheckImpact();
    }

    private void MoveAndCheckImpact()
    {
        Vector3 move = transform.forward * moveSpeed * Time.deltaTime;

        // Bu frame'de gideceðimiz yönde raycast ile yer var mý?
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
            move.magnitude + hitCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Çarpma
            Vector3 impactPoint = hit.point;
            photonView.RPC(nameof(RPC_OnImpact), RpcTarget.All, impactPoint);
        }
        else
        {
            transform.position += move;
        }
    }

    [PunRPC]
    private void RPC_OnImpact(Vector3 impactPoint)
    {
        if (hasImpacted) return;
        hasImpacted = true;

        // Pozisyonu tam çarpma noktasýna sabitle
        transform.position = impactPoint;

        // Biraz yer içine girmesin diye yukarý hafif kaldýrabilirsin:
        transform.position += Vector3.up * 0.2f;

        // FX
        if (impactFxPrefab != null)
        {
            GameObject fx = Instantiate(impactFxPrefab, impactPoint, Quaternion.identity);
            Destroy(fx, 5f);
        }

        // Ses (3D)
        if (impactAudioClip != null)
        {
            GameObject audioObj = new GameObject("MeteorImpactAudio");
            audioObj.transform.position = impactPoint;

            AudioSource src = audioObj.AddComponent<AudioSource>();
            src.clip = impactAudioClip;
            src.volume = impactVolume;
            src.spatialBlend = 1f; // 3D
            src.minDistance = impactMinDistance;
            src.maxDistance = impactMaxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();

            Destroy(audioObj, impactAudioClip.length + 0.2f);
        }

        // 10 sn sonra elmas çýkart
        StartCoroutine(DiamondRevealRoutine());
    }

    private IEnumerator DiamondRevealRoutine()
    {
        yield return new WaitForSeconds(diamondRevealDelay);

        if (meteorRoot != null) meteorRoot.SetActive(false);
        if (diamondRoot != null) diamondRoot.SetActive(true);

        // Elmas artýk senin MineableResource sisteminle toplanabilir olacak
        // (diamondRoot içine zaten Elmas cevheri prefab'ýný koyacaksýn)
    }
}
