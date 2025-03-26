using UnityEngine;

// By B0N3head 
// All yours, use this script however you see fit, feel free to give credit if you want
[AddComponentMenu("Player Movement and Camera Controller")]
public class PlayerMovement : MonoBehaviour
{
    [Header("Camera Settings")]

    [Tooltip("Lock the cursor to the game screen on play")]
    public bool lockCursor = true;
    [Tooltip("Clamp the camera angle (Stop the camera form \"snapping its neck\")")]
    public Vector2 clampInDegrees = new Vector2(360f, 180f);
    [Tooltip("The mouse sensitivity, both x and y")]
    public Vector2 sensitivity = new Vector2(2f, 2f);
    [Tooltip("Smoothing of the mouse movement (Try with and without)")]
    public Vector2 smoothing = new Vector2(1.5f, 1.5f);
    [Tooltip("Needs to be the same name as your main cam")]
    public string cameraName = "Camera";

    //----------------------------------------------------
    [Space]
    [Header("Movement Settings")]

    [Tooltip("Max walk speed")]
    public float walkMoveSpeed = 7.5f;
    [Tooltip("Max sprint speed")]
    public float sprintMoveSpeed = 11f;
    [Tooltip("Max jump speed")]
    public float jumpMoveSpeed = 6f;
    [Tooltip("Max crouch speed")]
    public float crouchMoveSpeed = 4f;

    //----------------------------------------------------
    [Header("Crouch Settings")]

    [Tooltip("How long it takes to crouch")]
    public float crouchDownSpeed = 0.2f;
    [Tooltip("How tall the character is when they crouch")]
    public float crouchHeight = 0.68f; //change for how large you want when crouching
    [Tooltip("How tall the character is when they stand")]
    public float standingHeight = 1f;
    [Tooltip("Lerp between crouching and standing")]
    public bool smoothCrouch = true;
    [Tooltip("Can you crouch while in the air")]
    public bool jumpCrouching = true;

    //----------------------------------------------------
    [Header("Jump Settings")]

    [Tooltip("Initial jump force")]
    public float jumpForce = 110f;
    [Tooltip("Continuous jump force")]
    public float jumpAccel = 10f;
    [Tooltip("Max jump up time")]
    public float jumpTime = 0.6f;
    [Tooltip("How long you have to jump after leaving a ledge (seconds)")]
    public float coyoteTime = 0.2f;
    [Tooltip("How long I should buffer your jump input for (seconds)")]
    public float jumpBuffer = 0.1f;
    [Tooltip("How long do I have to wait before I can jump again")]
    public float jumpCooldown = 4.99f;
    [Tooltip("Fall quicker")]
    public float extraGravity = 0.1f;
    [Tooltip("The tag that will be considered the ground")]
    public string groundTag = "Ground";


    //----------------------------------------------------
    [Space]
    [Header("Keyboard Settings")]

    [Tooltip("The key used to jump")]
    public KeyCode jump = KeyCode.Space;
    [Tooltip("The key used to sprint")]
    public KeyCode sprint = KeyCode.LeftShift;
    [Tooltip("The key used to crouch")]
    public KeyCode crouch = KeyCode.Z;
    [Tooltip("The key used to toggle the cursor")]
    public KeyCode lockToggle = KeyCode.Q;

    //----------------------------------------------------
    [Space]
    [Header("Debug Info")]

    [Tooltip("Are we on the ground?")]
    public bool areWeGrounded = true;
    [Tooltip("Are we crouching?")]
    public bool areWeCrouching = false;
    [Tooltip("The current speed I should be moving at")]
    public float currentSpeed;
    public bool areWeSprinting;

    //----------------------------------------------------
    // Reference vars (These are the vars used in calculations, they don't need to be set by the user)
    private Rigidbody rb;
    public GameObject cam;
    Vector3 input = new Vector3();
    Vector2 _mouseAbsolute, _smoothMouse, targetDirection, targetCharacterDirection;
    private bool wantingToJump = false, wantingToCrouch = false, wantingToSprint = false, jumpCooldownOver = false;
    Vector3 lastHFMovement = new Vector3();
    Vector3 initialJumpVelocityVector = Vector3.zero;

    private bool isSprintJump = false;
    private bool canDoubleJump = false;
    private int jumpCount = 0;
    private const float NormalStrafingReduction = 1.8f;
    private const float SprintStrafingReduction = 1.0f;

    Vector3 forwardVel;
    Vector3 horizontalVel;
    Vector3 verticalVel;

    void Awake()
    {
        // Just set rb to the rigidbody of the gameobject containing this script
        rb = gameObject.GetComponent<Rigidbody>();
        // Try find our camera amongst the child objects
        //cam = gameObject.transform.Find(cameraName).gameObject;
        // Set the currentSpeed to walking as no keys should be pressed yet
        currentSpeed = walkMoveSpeed;

        // Set target direction to the camera's initial orientation.
        targetDirection = transform.localRotation.eulerAngles;
        // Set target direction for the character body to its inital state.
        targetCharacterDirection = transform.localRotation.eulerAngles;

    }


    private void Update()
    {
        // Update the camera pos
        cameraUpdate();

        // Move all input to Update(), then use given input on FixedUpdate()

        // WSAD movement
        input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        // Jump key
        wantingToJump = Input.GetKey(jump);
        // Crouch key
        wantingToCrouch = Input.GetKey(crouch);
        // Sprint key
        wantingToSprint = Input.GetKey(sprint);

        // Mouse lock toggle (KeyDown only fires once)
        if (Input.GetKeyDown(lockToggle))
            lockCursor = !lockCursor;
    }

    public void cameraUpdate()
    {
        // Allow the script to clamp based on a desired target value.
        var targetOrientation = Quaternion.Euler(targetDirection);


        // Get raw mouse input for a cleaner reading on more sensitive mice.
        var mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

        // Scale input against the sensitivity setting and multiply that against the smoothing value.
        mouseDelta = Vector2.Scale(mouseDelta, new Vector2(sensitivity.x * smoothing.x, sensitivity.y * smoothing.y));

        // Interpolate mouse movement over time to apply smoothing delta.
        _smoothMouse.x = Mathf.Lerp(_smoothMouse.x, mouseDelta.x, 1f / smoothing.x);
        _smoothMouse.y = Mathf.Lerp(_smoothMouse.y, mouseDelta.y, 1f / smoothing.y);

        // Find the absolute mouse movement value from point zero.
        _mouseAbsolute += _smoothMouse;

        // Clamp and apply the local x value first, so as not to be affected by world transforms.
        if (clampInDegrees.x < 360)
            _mouseAbsolute.x = Mathf.Clamp(_mouseAbsolute.x, -clampInDegrees.x * 0.5f, clampInDegrees.x * 0.5f);

        // Then clamp and apply the global y value.
        if (clampInDegrees.y < 360)
            _mouseAbsolute.y = Mathf.Clamp(_mouseAbsolute.y, -clampInDegrees.y * 0.5f, clampInDegrees.y * 0.5f);

        // apply the cameras up & down rotation
        cam.transform.localRotation = Quaternion.AngleAxis(-_mouseAbsolute.y, targetOrientation * Vector3.right) * targetOrientation;
    }
    void ApplyPlayerRotation()
    {
        // apply player rotation around z axis
        var targetCharacterOrientation = Quaternion.Euler(targetCharacterDirection);
        var yRotation = Quaternion.AngleAxis(_mouseAbsolute.x, Vector3.up);
        transform.localRotation = yRotation * targetCharacterOrientation;
    }
    void FixedUpdate()
    {
        ApplyPlayerRotation();

        // Lock cursor handling
        if (lockCursor)
            Cursor.lockState = CursorLockMode.Locked;
        else
            Cursor.lockState = CursorLockMode.None;

        // Double check if we are on the ground or not (Changes current speed if true)
        if (Physics.Raycast(new Vector3(transform.position.x, transform.position.y - transform.localScale.y + 0.1f, transform.position.z), Vector3.down, 0.15f))
            handleHitGround();

        // Sprinting
        if (wantingToSprint && areWeGrounded && !areWeCrouching)
        {
            currentSpeed = sprintMoveSpeed;
            areWeSprinting = true;
        }
        else if (!areWeCrouching && areWeGrounded)
            currentSpeed = walkMoveSpeed;

        // Crouching 
        // Can be simplified to Crouch((wantingToCrouch && jumpCrouching)); though the bellow is more readable
        if (wantingToCrouch && jumpCrouching)
            Crouch(true);
        else
            Crouch(false);

        // WASD movement
        input = input.normalized;
        forwardVel = transform.forward * currentSpeed * input.z;
        horizontalVel = currentSpeed * input.x * transform.right;
        verticalVel = new Vector3(0, rb.velocity.y, 0);


        // Jumping
        if (wantingToJump && areWeGrounded)
        {
            if (areWeSprinting)
                isSprintJump = true;
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
            // wait a sec before grabbing the initial jump vel. vector
            //Invoke(nameof(initalJumpCountdown), 0.1f);


            areWeGrounded = false;
            currentSpeed = jumpMoveSpeed;
            jumpCount++;
            //prevent this frame from combining the initial jump and double jump 
            canDoubleJump = false;
        }
        //is in air and not holding down space the whole time
        else if (!wantingToJump && !areWeGrounded)
        {
            //give player the ability to double jump
            if (jumpCount < 2)
                canDoubleJump = true;
            else
                canDoubleJump = false;
        }
        //double jump
        else if (wantingToJump && !areWeGrounded && canDoubleJump)
        {
            rb.AddForce(transform.up * CalculateDoubleJumpForce(), ForceMode.Impulse);
            jumpCount++;
            canDoubleJump = false;
        }

        //Extra gravity for more nicer jumping
        //rb.AddForce(new Vector3(0, -extraGravity, 0), ForceMode.Impulse);


        if (!areWeGrounded)
        {
            HandleAirborne();
        }
        else
        {
            // Move character while grounded
            HandleGrounded();
        }

    }
    private float CalculateDoubleJumpForce(){
        // 110 N is jump force. 110 units per sec
        Debug.Log("Vertical vector:"+ verticalVel.y); // Independent of gravity
        float q = 11 - verticalVel.y;
        float res = (q / 11f) * jumpForce;
        Debug.Log("force:"+res);
        return res;
    }
    private void HandleAirborne()
    {
        /*
        if (jumpCooldownOver)
        {
            initialJumpVelocityVector = rb.velocity;
            jumpCooldownOver = false;
        }
        */
        CheckJumpDirectionChange();

        AdjustVelocityForStrafing();

        MaintainMomentumInAir();
    }

    private void CheckJumpDirectionChange()
    {
        if (initialJumpVelocityVector != Vector3.zero && HasJumpDirectionChanged(initialJumpVelocityVector, rb.velocity))
        {
            Debug.Log("Direction Changed!!!");
        }
    }

    private void AdjustVelocityForStrafing()
    {
        float strafingReduction = isSprintJump ? SprintStrafingReduction : NormalStrafingReduction;

        //forwardVel /= strafingReduction;
        //horizontalVel /= strafingReduction;
    }

    private void MaintainMomentumInAir()
    {
        if (forwardVel + horizontalVel == Vector3.zero)
        {
            rb.velocity = lastHFMovement + verticalVel;
        }
        else
        {
            //var forwardReduction = forwardVel.z > 0 ? 1f : NormalStrafingReduction;
            rb.velocity = (horizontalVel / NormalStrafingReduction) + (forwardVel / NormalStrafingReduction) + verticalVel;
            lastHFMovement = (horizontalVel / NormalStrafingReduction) + (forwardVel / NormalStrafingReduction);
        }
    }

    private void HandleGrounded()
    {
        rb.velocity = horizontalVel + forwardVel + verticalVel;
        ResetGroundedState();
    }

    private void ResetGroundedState()
    {
        lastHFMovement = Vector3.zero;
        isSprintJump = false;
        initialJumpVelocityVector = Vector3.zero;
    }
    private void initalJumpCountdown()
    {
        jumpCooldownOver = true;
    }
    // while in air, check if the initial jump vector is different from the current vel. vector
    private bool HasJumpDirectionChanged(Vector3 init, Vector3 current, float threshold = 0.9999f)
    {
        // Ignore the y-axis
        init.y = 0;
        current.y = 0;
        Vector3 initNormalized = init.normalized;
        Vector3 currentNormalized = current.normalized;

        float dotProduct = Vector3.Dot(initNormalized, currentNormalized);

        Debug.Log("Dot Product: " + dotProduct);

        return dotProduct < threshold;
    }
    // Crouch handling
    private void Crouch(bool crouch)
    {
        areWeCrouching = crouch;

        if (crouch)
        {
            // If the player is crouching
            currentSpeed = crouchMoveSpeed;

            if (smoothCrouch)
            {
                transform.localScale = new Vector3(transform.localScale.x, Mathf.Lerp(transform.localScale.y, crouchHeight, crouchDownSpeed), transform.localScale.z);
                transform.position = Vector3.Lerp(transform.position, new Vector3(transform.position.x, transform.position.y - crouchHeight, transform.position.z), crouchDownSpeed);
            }
            else if (transform.localScale != new Vector3(transform.localScale.x, crouchHeight, transform.localScale.z))
            {
                transform.localScale = new Vector3(transform.localScale.x, crouchHeight, transform.localScale.z);
                transform.position = new Vector3(transform.position.x, transform.position.y - crouchHeight / 2, transform.position.z);
            }
        }
        else
        {
            // If the player is standing
            if (smoothCrouch)
            {
                transform.localScale = new Vector3(transform.localScale.x, Mathf.Lerp(transform.localScale.y, standingHeight, crouchDownSpeed), transform.localScale.z);
                transform.position = Vector3.Lerp(transform.position, new Vector3(transform.position.x, transform.position.y - standingHeight / 2, transform.position.z), crouchDownSpeed);
            }
            else if (transform.localScale != new Vector3(transform.localScale.x, standingHeight, transform.localScale.z))
            {
                transform.localScale = new Vector3(transform.localScale.x, standingHeight, transform.localScale.z);
                transform.position = new Vector3(transform.position.x, transform.position.y + standingHeight / 2, transform.position.z);
            }
        }
    }

    // Ground check
    //****** make sure whatever you want to be the ground in your game matches the tag set in the script
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.tag == groundTag)
            handleHitGround();
    }

    // This is separated in its own void as this code needs to be run on two separate occasions, saves copy pasting code
    // Just double checking if we are crouching and setting the speed accordingly 
    public void handleHitGround()
    {
        if (areWeCrouching)
            currentSpeed = crouchMoveSpeed;
        else
            currentSpeed = walkMoveSpeed;

        areWeGrounded = true;
        jumpCount = 0;
    }

    // Dw about understanding this, it's just the code for setting up the player character 
    public void setupCharacter()
    {
        gameObject.tag = "Player";
        if (!gameObject.GetComponent<Rigidbody>())
        {
            Rigidbody rb = gameObject.AddComponent(typeof(Rigidbody)) as Rigidbody;
            rb.mass = 10;
        }
        else Debug.Log("Rigidbody already exists");

        if (!gameObject.transform.Find("Camera"))
        {
            Vector3 old = transform.position;
            gameObject.transform.position = new Vector3(0, -0.8f, 0);
            GameObject go = new GameObject("Camera");
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.transform.rotation = new Quaternion(0, 0, 0, 0);
            go.transform.localScale = new Vector3(1, 1, 1);
            go.transform.parent = transform;
            gameObject.transform.position = old;
            Debug.Log("Camera created");
        }
        else Debug.Log("Camera already exists");
    }
}