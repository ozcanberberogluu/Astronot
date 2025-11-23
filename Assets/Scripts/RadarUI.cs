using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarUI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public RectTransform radarRect;
    public RectTransform blipContainer;
    public RectTransform sweepTransform;
    public CanvasGroup canvasGroup;

    [Header("Blip Prefabs")]
    public GameObject blipPlayerPrefab;
    public GameObject blipShipPrefab;
    public GameObject blipOtherPrefab;

    [Header("Settings")]
    public float radarRange = 100f;
    public float sweepSpeed = 180f;

    private readonly Dictionary<RadarTarget, RectTransform> blipDict =
        new Dictionary<RadarTarget, RectTransform>();


    void Update()
    {
        bool visible = Input.GetKey(KeyCode.Tab);

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;

        if (sweepTransform != null)
            sweepTransform.Rotate(0f, 0f, -sweepSpeed * Time.unscaledDeltaTime);

        if (!visible || player == null)
            return;

        SyncBlips();
        UpdateBlipPositions();
    }



    void SyncBlips()
    {
        foreach (var target in RadarTarget.AllTargets)
        {
            if (target == null) continue;
            if (target.transform == player) continue;
            if (blipDict.ContainsKey(target)) continue;

            GameObject prefab = blipOtherPrefab;

            switch (target.type)
            {
                case RadarTargetType.Player: prefab = blipPlayerPrefab; break;
                case RadarTargetType.Ship: prefab = blipShipPrefab; break;
            }

            GameObject blipGO = Instantiate(prefab, blipContainer);
            RectTransform rt = blipGO.GetComponent<RectTransform>();
            blipDict[target] = rt;
        }


        List<RadarTarget> toRemove = new List<RadarTarget>();

        foreach (var kvp in blipDict)
        {
            if (kvp.Key == null || !RadarTarget.AllTargets.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);

                toRemove.Add(kvp.Key);
            }
        }

        foreach (var t in toRemove)
            blipDict.Remove(t);
    }



    void UpdateBlipPositions()
    {
        float radarRadius = radarRect.rect.width * 0.5f;

        foreach (var kvp in blipDict)
        {
            RadarTarget target = kvp.Key;
            RectTransform blipRect = kvp.Value;

            if (target == null || blipRect == null) continue;

            Vector3 offset3D = target.transform.position - player.position;
            Vector2 offset = new Vector2(offset3D.x, offset3D.z);

            float distance = offset.magnitude;

            if (distance > radarRange)
            {
                blipRect.gameObject.SetActive(false);
                continue;
            }

            blipRect.gameObject.SetActive(true);


            // ✨ ÇOK ÖNEMLİ: yön düzeltme
            Vector3 fwd = player.forward;
            Vector2 fwd2D = new Vector2(fwd.x, fwd.z).normalized;

            float fwdAngle = Mathf.Atan2(fwd2D.x, fwd2D.y);

            float cos = Mathf.Cos(-fwdAngle);
            float sin = Mathf.Sin(-fwdAngle);

            float rx = offset.x * cos - offset.y * sin;
            float ry = offset.x * sin + offset.y * cos;
            Vector2 rotated = new Vector2(rx, ry);


            float normalized = distance / radarRange;
            Vector2 radarPos = rotated.normalized * (normalized * radarRadius);

            blipRect.anchoredPosition = radarPos;
        }
    }
}
