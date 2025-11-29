using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShoppingCartCollector : MonoBehaviour
{
    private ShoppingCart cart;

    private void Awake()
    {
        cart = GetComponentInParent<ShoppingCart>();

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (cart == null) return;

        OreChunk chunk = other.GetComponentInParent<OreChunk>();
        if (chunk == null) return;

        cart.RegisterChunk(chunk);
    }

    private void OnTriggerExit(Collider other)
    {
        if (cart == null) return;

        OreChunk chunk = other.GetComponentInParent<OreChunk>();
        if (chunk == null) return;

        cart.UnregisterChunk(chunk);
    }
}
