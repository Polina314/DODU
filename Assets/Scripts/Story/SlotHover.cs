using UnityEngine;
using UnityEngine.EventSystems;

// Подсветка клетки при наведении мыши
public class SlotHover : MonoBehaviour, IPointerEnterHandler
{
    public InventoryUI owner;
    public int index;

    public void OnPointerEnter(PointerEventData e)
    {
        if (owner != null) owner.Select(index);
    }
}
