﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Actor))]
[RequireComponent(typeof(Animator))]
/// <summary>
/// This is a component used alongside Actors that controls all the movement and physics operations
/// </summary>
public class Controller2D : MonoBehaviour {
    // Info that can be used in other classes
    public static readonly string GROUND_LAYER = "Ground";
    public static readonly string PLATFORM_LAYER = "Platform";
    public static readonly string PLAYER_LAYER = "Player";
    public static readonly string ENEMY_LAYER = "Enemy";
    public static readonly float GRAVITY = -50f;
    public static readonly float KNOCK_BACK_REDUCTION = 10f;
    // Colision parameters
    private static readonly float BOUNDS_EXPANSION = -2;
    private static readonly float RAY_COUNT = 4;
    public static readonly float SKIN_WIDTH = 0.015f;
    public static readonly float FALLTHROUGH_DELAY = 0.2f;
    // Animation attributes and names
    private static readonly string ANIMATION_H_SPEED = "hSpeed";
    private static readonly string ANIMATION_V_SPEED = "vSpeed";
    private static readonly string ANIMATION_JUMP = "jump";

    // Other Componenents
    private Actor actor;
    private BoxCollider2D myCollider;
    private Animator animator;

    // Physics properties
    private RaycastOrigins raycastOrigins;
    private CollisionInfo collisions;
    private float hSpeed = 0;
    private float vSpeed = 0;
    private float horizontalRaySpacing;
    private float verticalRaySpacing;
    private float gravityScale = 1;
    private RaycastHit2D verticalHit;
    private RaycastHit2D horizontalHit;
    private bool ignorePlatformCollisions;

    // Public propoerties
    public bool FacingRight { get; set; } // false == left, true == right
    public bool OnLadder { get; set; }
    public bool KnockedBack { get; set; }

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start() {
        actor = GetComponent<Actor>();
        myCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        KnockedBack = false;
        OnLadder = false;
        CalculateSpacing();
    }

    // Update is called once per frame
    void Update() {
        Move();
        HandleKnockback();
    }

    /// <summary>
    /// Calculates the spacing only once based on how many rays will be used
    /// </summary>
    void CalculateSpacing() {
        Bounds bounds = myCollider.bounds;
        bounds.Expand(SKIN_WIDTH * BOUNDS_EXPANSION);
        horizontalRaySpacing = bounds.size.y / (RAY_COUNT - 1);
        verticalRaySpacing = bounds.size.x / (RAY_COUNT - 1);
    }

    /// <summary>
    /// The origin of each raycast must be updated every time before checking collisions
    /// </summary>
    void UpdateRaycastOrigins() {
        Bounds bounds = myCollider.bounds;
        bounds.Expand(SKIN_WIDTH * BOUNDS_EXPANSION);
        raycastOrigins.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        raycastOrigins.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        raycastOrigins.topLeft = new Vector2(bounds.min.x, bounds.max.y);
        raycastOrigins.topRight = new Vector2(bounds.max.x, bounds.max.y);

    }

    /*-------------------------*/
    /*--------MOVEMENT---------*/
    /*-------------------------*/

    /// <summary>
    /// Tries to move according to current speed and checking for collisions
    /// </summary>
    public void Move() {
        UpdateRaycastOrigins();
        collisions.Reset();
        vSpeed += GRAVITY * gravityScale * Time.deltaTime;
        if (horizontalHit && actor.canWallSlide && vSpeed < 0) {
            vSpeed = -actor.wallSlideVelocity;
        }
        Vector2 velocity = new Vector2(hSpeed * Time.deltaTime, vSpeed * Time.deltaTime);
        if (velocity.x != 0) {
            HorizontalCollisions(ref velocity);
        } else {
            horizontalHit = new RaycastHit2D();
        }
        if (velocity.y != 0) {
            VerticalCollisions(ref velocity);
        }
        transform.Translate(velocity);
        // Resets horizontal velocity on collision
        if (horizontalHit) {
            hSpeed = 0;
        }
        // Checks for ground and ceiling, resets jumps if grounded
        if (verticalHit) {
            if (collisions.below) actor.extraJumps = actor.maxExtraJumps;
            vSpeed = 0;
        }
        animator.SetFloat(ANIMATION_H_SPEED, hSpeed);
        animator.SetFloat(ANIMATION_V_SPEED, vSpeed);
    }

    /// <summary>
    /// Checks for collisions in the horizontal axis and adjust the speed accordingly to stop at the 
    /// collided object.
    /// </summary>
    /// <param name="velocity">The current object velocity used for the raycast lenght</param>
    private void HorizontalCollisions(ref Vector2 velocity) {
        float directionX = Mathf.Sign(velocity.x);
        float rayLength = Mathf.Abs(velocity.x) + SKIN_WIDTH;
        horizontalHit = new RaycastHit2D();
        for (int i = 0; i < RAY_COUNT; i++) {
            Vector2 rayOrigin = directionX == -1 ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
            rayOrigin += Vector2.up * (horizontalRaySpacing * i);
            horizontalHit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX,
                rayLength, LayerMask.GetMask(GROUND_LAYER));
            Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength, Color.red);
            if (horizontalHit) {
                velocity.x = (horizontalHit.distance - SKIN_WIDTH) * directionX;
                rayLength = horizontalHit.distance;
                collisions.left = directionX < 0;
                collisions.right = directionX > 0;
            }
        }
    }

    /// <summary>
    /// Checks for collisions in the vertical axis and adjust the speed accordingly to stop at the 
    /// collided object.
    /// </summary>
    /// <param name="velocity">The current object velocity used for the raycast lenght</param>
    private void VerticalCollisions(ref Vector2 velocity) {
        float directionY = Mathf.Sign(velocity.y);
        float rayLength = Mathf.Abs(velocity.y) + SKIN_WIDTH;
        verticalHit = new RaycastHit2D();
        for (int i = 0; i < RAY_COUNT; i++) {
            Vector2 rayOrigin = directionY == -1 ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i + velocity.x);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY,
                rayLength, LayerMask.GetMask(GROUND_LAYER));
            Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);
            // for jump-through platforms
            if (!ignorePlatformCollisions && directionY < 0 && !hit) {
                hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY,
                    rayLength, LayerMask.GetMask(PLATFORM_LAYER));
            }
            if (hit) {
                velocity.y = (hit.distance - SKIN_WIDTH) * directionY;
                rayLength = hit.distance;
                collisions.above = directionY > 0;
                collisions.below = directionY < 0;
                verticalHit = hit;
            }
        }
    }

    /// <summary>
    /// Tries to move the actor horizontally based on it's current movespeed and input pressure 
    /// while checking for movement impairments
    /// </summary>
    /// <param name="direction">-1 to 1; negative values = left; positive values = right</param>
    public void Walk(float direction) {
        if (CanMove()) {
            if (direction < 0)
                FacingRight = false;
            if (direction > 0)
                FacingRight = true;
            float acc = 0f;
            float dec = 0f;
            if (actor.advancedAirControl && !collisions.below) {
                acc = actor.airAccelerationTime;
                dec = actor.airDecelerationTime;
            } else {
                acc = actor.accelerationTime;
                dec = actor.decelerationTime;
            }
            if (acc > 0) {
                hSpeed += direction * (1 / acc) * actor.maxSpeed * Time.deltaTime;
            } else {
                hSpeed = actor.maxSpeed * direction;
            }
            if (direction == 0 || Mathf.Sign(direction) != Mathf.Sign(hSpeed)) {
                if (dec > 0) {
                    hSpeed = Mathf.MoveTowards(hSpeed, 0, (1 / dec) * actor.maxSpeed * Time.deltaTime);
                } else {
                    hSpeed = 0;
                }
            }
            hSpeed = Mathf.Clamp(hSpeed, -actor.maxSpeed, actor.maxSpeed);
        }
    }

    /// <summary>
    ///  Makes the actor jump if possible
    /// </summary>
    public void Jump() {
        if (CanMove()) {
            if (collisions.below || actor.extraJumps > 0 || (actor.canWallJump && horizontalHit)) {
                vSpeed = Mathf.Sqrt(2 * Mathf.Abs(GRAVITY) * actor.jumpHeight);
                animator.SetTrigger(ANIMATION_JUMP);
                // wall jump
                if (actor.canWallJump && horizontalHit && !collisions.below) {
                    hSpeed = collisions.left ? actor.wallJumpVelocity : -actor.wallJumpVelocity;
                    actor.extraJumps = actor.maxExtraJumps;
                } else {
                    // air jump
                    if (!collisions.below)
                        actor.extraJumps--;
                }
                RestorePlatformCollisions();
            }
        }
    }

    /// <summary>
    /// If the actor is standing on a platform, will ignore platforms briefly,
    /// otherwise it will just jump
    /// </summary>
    public void JumpDown() {
        if (CanMove()) {
            if (verticalHit && verticalHit.collider.gameObject.layer == LayerMask.NameToLayer(PLATFORM_LAYER)) {
                ignorePlatformCollisions = true;
                Invoke("RestorePlatformCollisions", FALLTHROUGH_DELAY);
            } else {
                Jump();
            }
        }
    }

    /// <summary>
    /// Restores plaftorm collisions after the set amount of time has passed
    /// or the actor has jumped
    /// </summary>
    private void RestorePlatformCollisions() {
        ignorePlatformCollisions = false;
    }

    /// <summary>
    /// Used to alter gravity strength for jump hold or other effects
    /// </summary>
    /// <param name="gravityScale">Desired gravity scale</param>
    public void SetGravityScale(float gravityScale) {
        this.gravityScale = gravityScale;
    }

    /// <summary>
    /// Handles knockback force and movement
    /// </summary>
    public void HandleKnockback() {
        if (KnockedBack) {
            float directionX = Mathf.Sign(hSpeed);
            float newHSpeed = Mathf.Abs(hSpeed) - (KNOCK_BACK_REDUCTION * Time.deltaTime);
            if (newHSpeed <= 0) {
                newHSpeed = 0;
                KnockedBack = false;
            }
            hSpeed = newHSpeed * directionX;
        }
    }

    /// <summary>
    /// Checks if there are any knockbacks or status that stop movement
    /// </summary>
    /// <returns>Whether the actor can move or not</returns>
    public bool CanMove() {
        return (!KnockedBack);
    }

    // Used to store temporary locations of raycast origins (the corners of the collider)
    struct RaycastOrigins {
        public Vector2 topLeft, topRight, bottomLeft, bottomRight;
    }

    // Stores temporary collision info to be used during calculations
    struct CollisionInfo {
        public bool above, below, left, right;

        public void Reset() {
            above = below = left = right = false;
        }
    }
}