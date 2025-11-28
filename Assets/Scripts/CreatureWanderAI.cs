using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PhotonView))]
public class CreatureWanderAI : MonoBehaviourPun
{
    [Header("Wander Settings")]
    [Tooltip("Yaratığın gezinebileceği maksimum yarıçap (mevcut pozisyondan). Büyük haritalar için artırın.")]
    public float wanderRadius = 300f;  // Büyük harita için artırıldı
    [Tooltip("Idle süresi (saniye). Yürüdükten sonra ne kadar beklesin.")]
    public float idleDuration = 4f;  // Sabit 4 saniye idle
    [Tooltip("Minimum hareket mesafesi. Bu mesafeden yakın hedefler seçilmez (büyük yaratıklar için artırın).")]
    public float minMoveDistance = 80f;   // Büyük yaratık için uzak hedefler
    [Tooltip("NavMesh araması için kullanılacak maksimum mesafe (wanderRadius'dan büyük olmalı).")]
    public float navMeshSearchRadius = 500f;  // Büyük harita için artırıldı

    [Header("Rotation")]
    [Tooltip("Yürürken hedef yöne dönme hızı (daha düşük = daha yumuşak dönüş).")]
    public float turnSpeed = 1.2f;         // Daha yumuşak dönüş için düşürüldü
    [Tooltip("Rotasyon için minimum velocity (çok düşük hızlarda dönme yapma).")]
    public float minVelocityForRotation = 1f;  // Daha yüksek threshold - sadece gerçekten hareket ederken dön
    [Tooltip("Rotasyon için minimum açı (bu açıdan küçük dönüşler yapılmaz - titreme önleme).")]
    public float minAngleForRotation = 2f;  // 2 dereceden küçük açılar için rotasyon yapma
    
    [Header("Path Timeout")]
    [Tooltip("Hedefe ulaşamazsa ne kadar süre sonra iptal edilsin (saniye).")]
    public float pathTimeout = 15f;  // 15 saniye sonra timeout

    [Header("Step Shake Settings")]
    [Tooltip("Yaratık adımlarının etkili olacağı maksimum mesafe.")]
    public float stepShakeRadius = 25f;

    [Tooltip("Yakında hissedilecek en güçlü sarsıntı.")]
    public float maxShake = 0.6f;

    [Tooltip("StepShakeRadius sınırında bile hissedilecek minimum sarsıntı.")]
    public float minShake = 0.06f;

    [Tooltip("Her adım sarsıntısının süresi.")]
    public float stepShakeDuration = 0.18f;

    [Header("Footstep Audio")]
    public AudioClip footstepClip;
    public float footstepVolume = 1f;
    public float footstepMinDistance = 3f;
    public float footstepMaxDistance = 40f;

    private NavMeshAgent agent;
    private Animator animator;

    private float idleTimer;
    private bool isIdle = true;
    private int failedDestinationAttempts = 0;  // Başarısız hedef arama sayacı
    private const int maxFailedAttempts = 5;    // Maksimum başarısız deneme sayısı
    
    private float pathStartTime = 0f;  // Path başladığında zaman
    private Vector3 lastPosition;     // Son pozisyon (takılma kontrolü için)
    private float stuckTimer = 0f;     // Takılma süresi
    private const float stuckThreshold = 3f;  // 3 saniye hareket etmezse takılı sayılır
    
    private Vector3 smoothedVelocity = Vector3.zero;  // Yumuşatılmış velocity (rotasyon için)
    private float velocitySmoothing = 5f;  // Velocity smoothing hızı
    
    // Pozisyon bazlı animasyon kontrolü için
    private Vector3 animCheckPosition;  // Animasyon kontrolü için son pozisyon
    private float positionCheckInterval = 0.1f;  // Pozisyon kontrolü aralığı (saniye)
    private float positionCheckTimer = 0f;  // Pozisyon kontrolü timer'ı
    private const float minPositionChange = 0.05f;  // Minimum pozisyon değişimi (Walk için)

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        // NavMeshAgent kendi rotasyonunu güncellemesin, biz kontrol edeceğiz
        agent.updateRotation = false;
        
        // NavMeshAgent'ın pozisyon güncellemesini kontrol et
        agent.updatePosition = true;
    }

    private void Start()
    {
        lastPosition = transform.position;
        animCheckPosition = transform.position;  // Animasyon kontrolü için başlangıç pozisyonu
        EnterIdle();
    }

    private void Update()
    {
        // AI sadece master client'ta çalışsın;
        // diğer client'larda transform/animasyon Photon ile sync olsun
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            UpdateAnimatorBasedOnPosition();  // Pozisyon bazlı animasyon kontrolü
            return;
        }

        if (isIdle)
        {
            // Idle durumunda agent'ı tamamen durdur
            if (agent.enabled && agent.hasPath)
            {
                agent.ResetPath();
            }
            
            // Idle durumunda velocity'yi sıfırla (animasyon için)
            if (agent.enabled && agent.velocity.sqrMagnitude > 0.01f)
            {
                agent.velocity = Vector3.zero;
            }
            
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleDuration)  // Sabit 4 saniye idle
            {
                TrySetNewDestination();
            }
        }
        else
        {
            // Yürürken yeni hedef arama YAPMA - sadece mevcut hedefe git
            // Bu kararsız davranışı önler
            
            // Path timeout kontrolü
            if (pathStartTime > 0f && Time.time - pathStartTime > pathTimeout)
            {
                Debug.LogWarning("FootsMonster: Path timeout! Hedefe ulaşılamadı, yeni hedef aranıyor...");
                EnterIdle();
                return;
            }
            
            // Path status kontrolü - path başarısız olduysa
            if (agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid || 
                agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial)
            {
                Debug.LogWarning("FootsMonster: Path geçersiz veya kısmi! Yeni hedef aranıyor...");
                EnterIdle();
                return;
            }
            
            // Takılma kontrolü - eğer uzun süredir aynı yerdeyse
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved < 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > stuckThreshold)
                {
                    Debug.LogWarning("FootsMonster: Takıldı! Yeni hedef aranıyor...");
                    EnterIdle();
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;  // Hareket ediyor, takılma sayacını sıfırla
                lastPosition = transform.position;
            }
            
            // Normal hedefe ulaşma kontrolü
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                EnterIdle();
            }
            
            // Velocity smoothing - rotasyon için daha stabil velocity
            if (agent.enabled && agent.velocity.sqrMagnitude > 0.01f)
            {
                smoothedVelocity = Vector3.Lerp(smoothedVelocity, agent.velocity, velocitySmoothing * Time.deltaTime);
            }
            else
            {
                smoothedVelocity = Vector3.Lerp(smoothedVelocity, Vector3.zero, velocitySmoothing * Time.deltaTime);
            }
        }

        HandleRotation();
        UpdateAnimatorBasedOnPosition();  // Pozisyon bazlı animasyon kontrolü
    }

    private void EnterIdle()
    {
        isIdle = true;
        idleTimer = 0f;  // Sabit 4 saniye idle
        
        // Path timeout ve takılma sayaçlarını sıfırla
        pathStartTime = 0f;
        stuckTimer = 0f;
        lastPosition = transform.position;
        
        // Animasyon kontrolü için pozisyonu kaydet
        animCheckPosition = transform.position;
        
        // Velocity smoothing'i sıfırla
        smoothedVelocity = Vector3.zero;

        if (agent.enabled)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;  // Idle'da velocity'yi sıfırla
        }
        
        // Hedefe ulaşıldığında başarısız deneme sayacını sıfırla
        if (failedDestinationAttempts > 0)
        {
            failedDestinationAttempts = Mathf.Max(0, failedDestinationAttempts - 1);
        }
    }

    private void TrySetNewDestination()
    {
        // Birkaç kez deneme yap (retry mekanizması)
        int maxAttempts = 10;
        float currentSearchRadius = navMeshSearchRadius;
        
        // Eğer önceki denemeler başarısız olduysa, daha geniş alanda ara
        if (failedDestinationAttempts > maxFailedAttempts)
        {
            currentSearchRadius = navMeshSearchRadius * 2f;  // İki katına çıkar
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Her denemede farklı bir yön seç - daha uzak mesafeler için
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            // Minimum mesafeyi de hesaba katarak uzak hedefler seç
            float minDistance = minMoveDistance;
            float maxDistance = wanderRadius;
            float distance = Random.Range(minDistance, maxDistance);
            
            Vector3 randomDir = new Vector3(
                transform.position.x + Mathf.Cos(angle) * distance,
                transform.position.y,
                transform.position.z + Mathf.Sin(angle) * distance
            );

            // NavMesh'te geçerli bir pozisyon ara
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, currentSearchRadius, NavMesh.AllAreas))
            {
                float distanceToTarget = Vector3.Distance(transform.position, hit.position);
                
                // Minimum mesafe kontrolü - büyük yaratık için uzak hedefler şart
                float effectiveMinDistance = minMoveDistance;
                if (failedDestinationAttempts > maxFailedAttempts)
                {
                    effectiveMinDistance = minMoveDistance * 0.7f;  // Biraz esnet ama çok değil
                }
                
                if (distanceToTarget < effectiveMinDistance)
                {
                    // Çok yakın, bir sonraki denemeye geç
                    continue;
                }

                // Geçerli bir hedef bulundu!
                if (agent.enabled)
                {
                    // Hedefin gerçekten ulaşılabilir olduğunu kontrol et
                    UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                    if (agent.CalculatePath(hit.position, path))
                    {
                        // Path başarılı, hedefi ayarla
                        agent.SetDestination(hit.position);
                        isIdle = false;
                        pathStartTime = Time.time;  // Path başlangıç zamanını kaydet
                        lastPosition = transform.position;  // Son pozisyonu kaydet
                        animCheckPosition = transform.position;  // Animasyon kontrolü için pozisyonu kaydet
                        stuckTimer = 0f;  // Takılma sayacını sıfırla
                        smoothedVelocity = Vector3.zero;  // Velocity smoothing'i sıfırla (yeni hedef için)
                        failedDestinationAttempts = 0;  // Başarılı oldu, sayacı sıfırla
                        return;
                    }
                    else
                    {
                        // Path hesaplanamadı, bu hedefi atla
                        continue;
                    }
                }
            }
        }

        // Tüm denemeler başarısız oldu
        failedDestinationAttempts++;
        
        // Sabit idle süresi kullan (4 saniye)
        Debug.LogWarning($"FootsMonster: {failedDestinationAttempts} başarısız hedef arama. Daha geniş alanda arıyor...");
        
        idleTimer = 0f;
    }

    private void HandleRotation()
    {
        // Idle durumunda hiçbir rotasyon yapma - kesinlikle dönme
        if (isIdle || agent == null) 
        {
            smoothedVelocity = Vector3.zero;  // Idle'da smoothed velocity'yi sıfırla
            return;
        }

        // Yumuşatılmış velocity kullan (daha stabil rotasyon için)
        Vector3 vel = smoothedVelocity;
        vel.y = 0f;

        // Velocity çok küçükse rotasyon yapma (kararsız dönüşleri önler)
        if (vel.sqrMagnitude < minVelocityForRotation * minVelocityForRotation) 
        {
            return;
        }

        // Hedef rotasyonu hesapla - sadece hareket yönüne dön
        Quaternion targetRot = Quaternion.LookRotation(vel.normalized, Vector3.up);
        
        // Mevcut rotasyon ile hedef rotasyon arasındaki açıyı kontrol et
        float angle = Quaternion.Angle(transform.rotation, targetRot);
        
        // Minimum açı kontrolü - çok küçük açılar için rotasyon yapma (titreme önleme)
        if (angle < minAngleForRotation)
        {
            return;
        }
        
        // Yumuşak dönüş için Slerp kullan - sadece hedefe doğru dön
        // Büyük açılarda biraz daha hızlı, küçük açılarda daha yavaş
        float dynamicTurnSpeed = turnSpeed;
        if (angle > 45f)
        {
            dynamicTurnSpeed = turnSpeed * 1.3f;  // Büyük açılarda biraz daha hızlı
        }
        else if (angle < 15f)
        {
            dynamicTurnSpeed = turnSpeed * 0.6f;  // Küçük açılarda daha yavaş (titreme önleme)
        }
        
        // Sadece hedefe doğru yumuşak dönüş
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            dynamicTurnSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Pozisyon bazlı animasyon kontrolü - gerçekçi ve güvenilir
    /// Pozisyon değişiyorsa Walk, değişmiyorsa Idle
    /// </summary>
    private void UpdateAnimatorBasedOnPosition()
    {
        if (animator == null) return;

        // Belirli aralıklarla pozisyon kontrolü yap (her frame değil, performans için)
        positionCheckTimer += Time.deltaTime;
        
        if (positionCheckTimer >= positionCheckInterval)
        {
            positionCheckTimer = 0f;
            
            // Mevcut pozisyon ile son kontrol pozisyonu arasındaki mesafeyi hesapla
            float positionChange = Vector3.Distance(transform.position, animCheckPosition);
            
            // Pozisyon değişiyorsa Walk, değişmiyorsa Idle
            if (positionChange > minPositionChange)
            {
                // Pozisyon değişiyor - Walk animasyonu
                animator.SetFloat("Speed", 1f);  // Walk için 1
                animCheckPosition = transform.position;  // Yeni pozisyonu kaydet
            }
            else
            {
                // Pozisyon değişmiyor - Idle animasyonu
                animator.SetFloat("Speed", 0f);  // Idle için 0
            }
        }
    }

    // ================= ANIMATION EVENT =================
    /// <summary>
    /// Walk animasyonunun adım frame'lerine animation event olarak bağla.
    /// </summary>
    public void OnFootstep()
    {
        // Adımı sadece master tetiklesin, sonra herkese RPC ile gitsin
        Vector3 stepPos = transform.position;

        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_OnFootstep), RpcTarget.All, stepPos);
        }
        else
        {
            RPC_OnFootstep(stepPos);
        }
    }

    [PunRPC]
    private void RPC_OnFootstep(Vector3 stepPosition)
    {
        // ---- 3D footstep sesi ----
        if (footstepClip != null)
        {
            GameObject audioObj = new GameObject("CreatureFootstepAudio");
            audioObj.transform.position = stepPosition;

            AudioSource src = audioObj.AddComponent<AudioSource>();
            src.clip = footstepClip;
            src.volume = footstepVolume;
            src.spatialBlend = 1f;                  // %100 3D
            src.minDistance = footstepMinDistance;
            src.maxDistance = footstepMaxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;

            src.Play();
            Destroy(audioObj, footstepClip.length + 0.1f);
        }

        // ---- Kamera sarsma ----
        PlayerCameraController[] cams = FindObjectsOfType<PlayerCameraController>();
        foreach (var cam in cams)
        {
            if (cam == null || cam.target == null) continue;

            float dist = Vector3.Distance(stepPosition, cam.target.position);
            if (dist > stepShakeRadius) continue;

            float normalized = 1f - (dist / stepShakeRadius);
            normalized = Mathf.Clamp01(normalized);

            float amp = Mathf.Lerp(minShake, maxShake, normalized);

            cam.ShakeCamera(amp, stepShakeDuration);
        }
    }
}
