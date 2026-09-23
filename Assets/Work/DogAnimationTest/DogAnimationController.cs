using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DogAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private static readonly int Bark = Animator.StringToHash("Bark");

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (IsBarkKeyPressed())
        {
            animator.SetTrigger(Bark);
        }
    }

    private static bool IsBarkKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.B);
#else
        return false;
#endif
    }
}
