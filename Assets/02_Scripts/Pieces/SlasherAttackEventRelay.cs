using UnityEngine;

public class SlasherAttackEventRelay : MonoBehaviour
{
    [SerializeField] private SlasherPiece owner;

    public void OnAttackAnimationEnd()
    {
        owner ??= GetComponentInParent<SlasherPiece>();
        if (owner == null)
        {
            return;
        }

        owner.OnAttackAnimationEnd();
    }
}
