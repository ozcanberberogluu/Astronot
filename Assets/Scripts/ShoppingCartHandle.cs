using UnityEngine;

public class ShoppingCartHandle : MonoBehaviour
{
    public ShoppingCart cart;

    private void Reset()
    {
        if (cart == null)
            cart = GetComponentInParent<ShoppingCart>();
    }
}
