using UnityEngine;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class GrappleReticle : MonoBehaviour
{
    [SerializeField] protected Sprite Image_Grappling;
    [SerializeField] protected Color Colour_Grappling = Color.blue;
    [SerializeField] protected Sprite Image_CanGrapple;
    [SerializeField] protected Color Colour_CanGrapple = Color.green;
    [SerializeField] protected Sprite Image_CannotGrapple;
    [SerializeField] protected Color Colour_CannotGrapple = Color.red;

    UnityEngine.UI.Image LinkedImage;

    void Awake()
    {
        LinkedImage = GetComponent<UnityEngine.UI.Image>();
    }

    void Start()
    {
        OnGrappleStatusUpdated(false, false);
    }

    public void OnGrappleStatusUpdated(bool bInIsGrappling, bool bInCanGrapple)
    {
        if (bInIsGrappling)
        {
            LinkedImage.sprite = Image_Grappling;
            LinkedImage.color = Colour_Grappling;
        }
        else if (bInCanGrapple)
        {
            LinkedImage.sprite = Image_CanGrapple;
            LinkedImage.color = Colour_CanGrapple;
        }
        else
        {
            LinkedImage.sprite = Image_CannotGrapple;
            LinkedImage.color = Colour_CannotGrapple;
        }
    }
}
