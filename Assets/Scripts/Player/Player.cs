using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
  [Header("General")]
  [SerializeField] private Rigidbody rb_;
  [SerializeField] private PlayerInput player_input_;

  [Header("Movement")]
  [SerializeField] private float start_speed_;
  // Drag curves dependent on speed
  [SerializeField] private float drag_power_;
  [SerializeField] private AnimationCurve forward_drag_;
  [SerializeField] private AnimationCurve backward_drag_;
  [SerializeField] private AnimationCurve right_drag_;
  [SerializeField] private AnimationCurve left_drag_;
  [SerializeField] private AnimationCurve up_drag_;
  [SerializeField] private AnimationCurve down_drag_;

  [SerializeField] private AnimationCurve vertical_lift_coefficient_;
  [SerializeField] private float vertical_lift_power_;
  [SerializeField] private float vertical_induced_drag_;
  [SerializeField] private float vertical_add_lift_power_;
  [SerializeField] private float vertical_lift_bias_;
  [SerializeField] private AnimationCurve horizontal_lift_coefficient_;
  [SerializeField] private float horizontal_lift_power_;
  [SerializeField] private float horizontal_induced_drag_;

  [Header("Steering")]
  [SerializeField] private Vector3 steer_speed_;
  [SerializeField] private Vector3 steer_acceleration_;
  [SerializeField] private AnimationCurve steering_power_;

  private Quaternion inverse_rotation_;
  private Vector3 local_velocity_;
  private Vector3 local_angular_velocity_;
  private float angle_of_attack_pitch_;
  private float angle_of_attack_yaw_;

  [Header("Restart")]
  [SerializeField] private Transform start_position_;

  [Header("Aesthetics")]

  [Header("Debug Text")]
  [SerializeField] private TextMeshProUGUI speed_text_;
  [SerializeField] private TextMeshProUGUI height_text_;
  [SerializeField] private TextMeshProUGUI vertical_speed_text_;
  [SerializeField] private TextMeshProUGUI drag_text_;
  [SerializeField] private TextMeshProUGUI induced_drag_text_;
  [SerializeField] private TextMeshProUGUI lift_text_;

  private bool text_enabled_ = true;
  private string drag_text_value_;
  private string lift_text_value_;
  private string induced_drag_text_value_;

  [Header("Debug")]
  [SerializeField] private bool draw_gizmos_;

  // Input
  private float pitch_input_;
  private float yaw_input_;
  private float roll_input_;
  

  private void Start()
  {
    rb_.AddForce(rb_.transform.forward * start_speed_, ForceMode.VelocityChange);
    player_input_.onActionTriggered += HandleInput;
  }

  private void Update()
  {
    if(draw_gizmos_)
      DrawGizmos();

    UpdateText();
  }

  private void FixedUpdate()
  {
    UpdateLocalVelocity();
    UpdateAngleOfAttack();
    ApplyDrag();
    ApplyLift();
    HandleSteering();
  }

  private void ApplyLift()
  {
    (Vector3 vertical_lift, Vector3 vertical_induced_drag) = CalculateLift(angle_of_attack_pitch_ + (Mathf.Deg2Rad * pitch_input_ * vertical_lift_bias_), Vector3.right, vertical_lift_coefficient_, vertical_lift_power_ + (vertical_add_lift_power_ * (pitch_input_ > 0 ? 1 : 0)), vertical_induced_drag_);
    lift_text_value_ = "Lift applied " + vertical_lift.ToString();
    (Vector3 horizontal_lift, Vector3 horizontal_induced_drag) = CalculateLift(angle_of_attack_yaw_, Vector3.up, horizontal_lift_coefficient_, horizontal_lift_power_, horizontal_induced_drag_);
    
    rb_.AddRelativeForce(vertical_lift + horizontal_lift + vertical_induced_drag + horizontal_induced_drag);
  }

  private void EnableText(bool enabled)
  {
    text_enabled_ = enabled;
    speed_text_.enabled = enabled;
    vertical_speed_text_.enabled = enabled;
    height_text_.enabled = enabled;
    drag_text_.enabled = enabled;
    induced_drag_text_.enabled = enabled;
    lift_text_.enabled = enabled;
  }

  private void UpdateText()
  {
    speed_text_.text = "Flat speed: " + new Vector2(local_velocity_.x, local_velocity_.z).magnitude.ToString();
    vertical_speed_text_.text = "Vertical speed: " + local_velocity_.y;
    height_text_.text = "Height: " + rb_.position.y.ToString();
    drag_text_.text = drag_text_value_;
    induced_drag_text_.text = induced_drag_text_value_;
    lift_text_.text = lift_text_value_;
  }

  private void HandleSteering()
  {
    float steering_power = steering_power_.Evaluate(Mathf.Max(local_velocity_.z, 0));
    Vector3 target_angular_velocity = new Vector3(pitch_input_ * steer_speed_.x, yaw_input_ * steer_speed_.y, roll_input_ * steer_speed_.z) * steering_power;
    Vector3 target_steer_acceleration = steer_acceleration_ * steering_power * Time.deltaTime;
    Vector3 current_angular_velocity = local_angular_velocity_ * Mathf.Rad2Deg;
    target_angular_velocity = new Vector3(Mathf.Clamp(target_angular_velocity.x - current_angular_velocity.x, -target_steer_acceleration.x, target_steer_acceleration.x),
                                          Mathf.Clamp(target_angular_velocity.y - current_angular_velocity.y, -target_steer_acceleration.y, target_steer_acceleration.y),
                                          Mathf.Clamp(target_angular_velocity.z - current_angular_velocity.z, -target_steer_acceleration.z, target_steer_acceleration.z));

    rb_.AddRelativeTorque(target_angular_velocity * Mathf.Deg2Rad);
  }

  private void ApplyDrag()
  {
    Vector3 coefficient = local_velocity_.normalized;
    coefficient = new Vector3(coefficient.x * (local_velocity_.x > 0 ? right_drag_.Evaluate(local_velocity_.x)   : left_drag_.Evaluate(-local_velocity_.x)),
                              coefficient.y * (local_velocity_.y > 0 ? up_drag_.Evaluate(local_velocity_.y)      : down_drag_.Evaluate(-local_velocity_.y)),
                              coefficient.z * (local_velocity_.z > 0 ? forward_drag_.Evaluate(local_velocity_.z) : backward_drag_.Evaluate(-local_velocity_.z)));
    rb_.AddRelativeForce(coefficient.magnitude * local_velocity_.sqrMagnitude * drag_power_ * -local_velocity_.normalized);

    drag_text_value_ = "Drag applied " + (coefficient.magnitude * local_velocity_.sqrMagnitude * drag_power_ * -local_velocity_.normalized).ToString();
  }

  private (Vector3 lift, Vector3 induced_drag) CalculateLift(float angle_of_attack, Vector3 plane_normal, AnimationCurve coefficient, float lift_power, float induced_drag)
  {
    Vector3 lift_velocity = Vector3.Max(local_velocity_, Vector3.zero);
    lift_velocity = Vector3.ProjectOnPlane(local_velocity_, plane_normal);
    Vector3 lift_direction = Vector3.Cross(lift_velocity, plane_normal);
    float lift_coeffcient = coefficient.Evaluate(angle_of_attack * Mathf.Rad2Deg);
    float lift_force = lift_velocity.sqrMagnitude * lift_coeffcient * lift_power;

    float induced_drag_force = lift_velocity.sqrMagnitude * lift_coeffcient * lift_coeffcient * induced_drag;
    Vector3 induced_drag_direction = -lift_velocity.normalized;

    return (lift_direction * lift_force, induced_drag_direction * induced_drag_force);
  }

  private void UpdateLocalVelocity()
  {
    inverse_rotation_ = Quaternion.Inverse(rb_.rotation);
    local_velocity_ = inverse_rotation_ * rb_.linearVelocity;
    local_angular_velocity_ = inverse_rotation_ * rb_.angularVelocity;
  }

  private void UpdateAngleOfAttack()
  {
    if(local_velocity_.sqrMagnitude < 0.1f)
    {
      angle_of_attack_pitch_ = 0;
      angle_of_attack_yaw_ = 0;
      return;
    }

    angle_of_attack_pitch_ = Mathf.Atan2(-local_velocity_.y, local_velocity_.z);
    angle_of_attack_yaw_ = Mathf.Atan2(local_velocity_.x, local_velocity_.z);
  }

  private void HandleInput(InputAction.CallbackContext input_context)
  {
    switch(input_context.action.name)
    {
    case "pitch":
      pitch_input_ = input_context.ReadValue<float>();
      break;
    case "yaw":
      yaw_input_ = input_context.ReadValue<float>();
      break;
    case "roll":
      roll_input_ = input_context.ReadValue<float>();
      break;
    case "Restart":
      Restart();
      break;
    case "Enable Debug Text":
      EnableText(!text_enabled_);
      break;
    default:
      Debug.LogError("Unhandled input action");
      break;
    }
  }

  private void Restart()
  {
    rb_.position = start_position_.position;
    rb_.rotation = Quaternion.identity;
    rb_.linearVelocity = Vector3.zero;
    rb_.angularVelocity = Vector3.zero;
    rb_.AddForce(rb_.transform.forward * start_speed_, ForceMode.VelocityChange);
  }

  private void DrawGizmos()
  {
  }
}