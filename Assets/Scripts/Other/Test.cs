using UnityEngine;
using UnityEngine.InputSystem;

public class Test : MonoBehaviour
{
  [SerializeField] private PlayerInput input_;

  private void Start()
  {
    input_.onActionTriggered += HandleInput;
  }

  private void HandleInput(InputAction.CallbackContext context)
  {
    if(context.action.name == "Move")
      Debug.Log(context.ReadValue<Vector2>());
  }
}
