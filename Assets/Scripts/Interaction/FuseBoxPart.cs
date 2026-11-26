using UnityEngine;

// Служебный: вешается САМ на дверцу и рубильники щитка (из FuseBox.cs). Руками добавлять не нужно.
// Передаёт нажатие E из вашей системы взаимодействия в FuseBox.
public class FuseBoxPart : MonoBehaviour, IInteractable
{
    [HideInInspector] public FuseBox box;
    [HideInInspector] public int index;     // -1 = дверца, 0..2 = рубильники

    public string GetPrompt() => box != null ? box.PromptFor(index) : "";
    public void Interact(PlayerInteraction player) { if (box != null) box.Use(index); }
}
