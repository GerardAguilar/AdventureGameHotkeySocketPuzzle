using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;


public class PlayerMovement : MonoBehaviour {

    public Animator animator;
    public NavMeshAgent agent;
    public float inputHoldDelay = .5f;//hold before using input
    public float turnSpeedThreshold = 0.5f;
    public float speedDampTime = 0.1f;
    public float slowingSpeed = 0.175f;
    public float turnSmoothing = 15f;//The higher, the faster the turn

    private WaitForSeconds inputHoldWait;
    private Vector3 destinationPosition;
    private Interactable currentInteractable;
    private bool handleInput = true;

    private const float stopDistanceProportion = 0.1f;//modifies the stopping distance
    private const float navMeshSampleDistance = 4f;//distance away from the click that the nav mesh can be.

    private readonly int hashSpeedPara = Animator.StringToHash("Speed");
    private readonly int hashLocomotionTag = Animator.StringToHash("Locomotion");

    private void Start() {
        //rotation not based on navmeshagent
        agent.updateRotation = false;

        inputHoldWait = new WaitForSeconds(inputHoldDelay);

        destinationPosition = transform.position; //initialize destinationPosition
    }

    private void OnAnimatorMove() {//how our character moves, combine NavMeshAgent and Animator
        agent.velocity = animator.deltaPosition / Time.deltaTime;//how far the character wants to move in a frame
    }

    private void Update() {
        if (agent.pathPending) {//if the agent is still making the path
            return;
        }

        float speed = agent.desiredVelocity.magnitude;

        if (agent.remainingDistance <= agent.stoppingDistance * stopDistanceProportion)
        {
            Stopping(out speed);
        }
        else if (agent.remainingDistance <= agent.stoppingDistance) {
            Slowing(out speed, agent.remainingDistance);
        } else if (speed > turnSpeedThreshold) {
            Moving();
        }

        animator.SetFloat(hashSpeedPara, speed, speedDampTime, Time.deltaTime);

    }

    private void Stopping(out float speed) {
        agent.Stop();
        transform.position = destinationPosition;//snap into target position
        speed = 0f;//actually stop

        if (currentInteractable) {
            transform.rotation = currentInteractable.interactionLocation.rotation;
            currentInteractable.Interact();
            currentInteractable = null;//so that interactable can only happen once.
            StartCoroutine(WaitForInteraction());//wait for interactable to finish
        }

    }

    private void Slowing(out float speed, float distanceToDestination) {
        agent.Stop();
        transform.position = Vector3.MoveTowards(transform.position, destinationPosition, slowingSpeed*Time.deltaTime);

        float proportionalDistance = 1f-distanceToDestination/agent.stoppingDistance;//at 0, propDist is 100%
        speed = Mathf.Lerp(slowingSpeed, 0f, proportionalDistance);//lower speed gradually

        Quaternion targetRotation =
            currentInteractable ? currentInteractable.interactionLocation.rotation : transform.rotation;

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, proportionalDistance);//Don't know what proportionalDistance does here, maybe turn speed based on how far the click is?
    }

    private void Moving() {
        Quaternion targetRotation = Quaternion.LookRotation(agent.desiredVelocity);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, turnSmoothing*Time.deltaTime);
        
    }

    public void OnGroundClick(BaseEventData data) {

        if (!handleInput) {
            return;
        }

        currentInteractable = null;

        PointerEventData pData = (PointerEventData)data;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            pData.pointerCurrentRaycast.worldPosition, //point of collider we hit
            out hit, //what we hit
            navMeshSampleDistance, //sample over
            NavMesh.AllAreas))//area to work with
        {
            destinationPosition = hit.position;//allow for better hits
        }
        else {
            destinationPosition = pData.pointerCurrentRaycast.worldPosition;
        }

        //move
        agent.SetDestination(destinationPosition);//need to tell agent to start again
        agent.Resume();
    }

    public void OnInteractableClick(Interactable interactable) {
        //Don't need to know where, since this will only get triggered when an interactable is clicked

        if (!handleInput) {//ignore inputs if you can't handle inputs yet
            return;
        }

        currentInteractable = interactable;

        destinationPosition = currentInteractable.interactionLocation.position;

        //move
        agent.SetDestination(destinationPosition);//need to tell agent to start again
        agent.Resume();
    }

    private IEnumerator WaitForInteraction() {

        handleInput = false;

        yield return inputHoldWait;//cache'd WaitForSeconds

        while (animator.GetCurrentAnimatorStateInfo(0).tagHash != hashLocomotionTag) {
            yield return null;//trap the flow into this until we have locomotion tag
        }

        handleInput = true;

    }



}
