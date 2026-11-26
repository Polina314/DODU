using System.Collections;
using UnityEngine;

// Управление дверью из событий: запереть, отпереть, захлопнуть.
// Вешается на любой объект (можно прямо на дверь). В поле Door — дверь со скриптом Door.
// Вторая створка (Linked Door) управляется вместе с первой.
public class DoorControl : MonoBehaviour
{
    public Door door;
    [Tooltip("Звук захлопывания (необязательно)")]
    public AudioSource audioSource;
    public AudioClip slamSound;
    [Tooltip("Надпись на запертой двери после захлопывания")]
    public string lockedPromptAfterSlam = "Заперто";

    void Awake()
    {
        if (door == null) door = GetComponent<Door>();
    }

    public void Unlock()
    {
        if (door == null) return;
        door.isLocked = false;
        if (door.linkedDoor != null) door.linkedDoor.isLocked = false;
    }

    public void Lock()
    {
        if (door == null) return;
        door.isLocked = true;
        if (door.linkedDoor != null) door.linkedDoor.isLocked = true;
    }

    // Дверь резко захлопывается сама и запирается
    public void Slam()
    {
        if (door == null) return;
        StartCoroutine(SlamRoutine());
    }

    IEnumerator SlamRoutine()
    {
        float s = door.speed;
        door.speed = 14f;
        door.SetOpen(false, transform.position);
        if (door.linkedDoor != null) { door.linkedDoor.speed = 14f; door.linkedDoor.SetOpen(false, transform.position); }
        if (audioSource != null && slamSound != null) audioSource.PlayOneShot(slamSound);
        Lock();
        door.lockedPrompt = lockedPromptAfterSlam;
        yield return new WaitForSeconds(0.6f);
        door.speed = s;
        if (door.linkedDoor != null) door.linkedDoor.speed = s;
    }
}
