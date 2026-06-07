using UnityEngine;

public class SlasherAttackEventRelay : MonoBehaviour
{
    [SerializeField] private SlasherPiece owner;

    public void OnAttackAnimationEnd()
    {
        if (owner == null)
        {
            owner = transform.parent.GetComponent<SlasherPiece>();
        }
        owner.OnAttackAnimationEnd();
    }
}
