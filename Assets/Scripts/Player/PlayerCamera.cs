using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
  [Header("General")]
  [SerializeField] private Camera cam_;
  [SerializeField] private Transform body_;
  [SerializeField] private Transform camera_position_;

  [Header("Movement")]
  [SerializeField] private float rotation_step_; // degrees per second
  [SerializeField] private float move_step_; // percent per second


  private void Update()
  {
    cam_.transform.position = body_.position + new Vector3(0, 3, -4);
    /*
    Vector3 diffrence = Quaternion.LookRotation(body_.position - cam_.transform.position).eulerAngles - cam_.transform.rotation.eulerAngles;
    // Angle between perpendicular vectors = 90. Distance between them sqrt(2) so to convert rotation step to correct unit I do * (sqrt(2)/90)
    // sqrt(2)/90=x/1
    cam_.transform.rotation = cam_.transform.rotation * Quaternion.Euler(Vector3.ClampMagnitude(diffrence, Time.deltaTime * rotation_step_ * (Mathf.Sqrt(2) / 90.0f)));

    cam_.transform.position = Vector3.Lerp(cam_.transform.position, camera_position_.position, Time.deltaTime * move_step_);
    */
  }
}