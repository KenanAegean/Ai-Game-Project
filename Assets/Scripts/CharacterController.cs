using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// _     __________    _    ______   __   ____ ___  ____  _____           
//| |   | ____/ ___|  / \  / ___\ \ / /  / ___/ _ \|  _ \| ____|          
//| |   |  _|| |  _  / _ \| |    \ V /  | |  | | | | | | |  _|            
//| |___| |__| |_| |/ ___ | |___  | |   | |__| |_| | |_| | |___           
//|_____|_____\____/_/   \_\____| |_|    \____\___/|____/|_____|          

// ____   ___    _   _  ___ _____    ____ _   _    _    _   _  ____ _____ 
//|  _ \ / _ \  | \ | |/ _ |_   _|  / ___| | | |  / \  | \ | |/ ___| ____|
//| | | | | | | |  \| | | | || |   | |   | |_| | / _ \ |  \| | |  _|  _|  
//| |_| | |_| | | |\  | |_| || |   | |___|  _  |/ ___ \| |\  | |_| | |___ 
//|____/ \___/  |_| \_|\___/ |_|    \____|_| |_/_/   \_|_| \_|\____|_____|

public class CharacterController : MonoBehaviour
{
    protected const float ReachDistThreshold = 0.1f;
    protected const float CharacterMoveSpeed = 1.0f;
    protected Animator myAnimator;

    protected Grid.Tile myCurrentTile = null;
    protected bool myReachedTile = false;
    protected bool myReachedDestination = false;
    protected List<Grid.Tile> myWalkBuffer = new List<Grid.Tile>();

    private Transform model = null;

    public virtual void UpdateCharacter()
    {
        if (myCurrentTile != null)
        {
            Vector3 tilePosition = Grid.Instance.WorldPos(myCurrentTile);
            myReachedTile = Vector3.Distance(transform.position, tilePosition) < ReachDistThreshold;
            transform.position = Vector3.MoveTowards(transform.position, tilePosition, CharacterMoveSpeed * Time.deltaTime);
        }

        myReachedDestination = myWalkBuffer.Count == 0;

        if (myWalkBuffer.Count > 0)
        {
            Grid.Tile t = myWalkBuffer.ElementAt(0);
            if (!t.occupied)
            {
                MoveTile(t);
            }

            myAnimator.SetBool("Walk", true);

            Vector3 targetPosition = Grid.Instance.WorldPos(t);
            Vector3 currentPosition = transform.position;
            Vector3 direction = (targetPosition - currentPosition).normalized;

            // Debug log to check the direction vector
            Debug.Log($"UpdateCharacter: targetPosition={targetPosition}, currentPosition={currentPosition}, direction={direction}");

            SetForward(direction);

            if (myReachedTile && myCurrentTile == t)
            {
                myWalkBuffer.RemoveAt(0);
                myReachedTile = false; // Reset reached tile for the next tile
            }
        }
        else if (myReachedTile)
        {
            myAnimator.SetBool("Walk", false);
        }
    }

    public virtual void StartCharacter() { }

    void Start()
    {
        myCurrentTile = Grid.Instance.GetClosest(transform.position);
        myAnimator = GetComponentInChildren<Animator>();

        if (myAnimator == null)
        {
            Debug.LogError("Animator component not found in children.");
        }

        // Ensure model is correctly assigned
        model = myAnimator.transform;

        if (model == null)
        {
            Debug.LogError("Model transform not found.");
        }
    }

    void SetForward(Vector3 forward)
    {
        Vector3 newForward = forward;
        newForward.y = 0;  // Keep the character facing horizontally

        // Debug log to check the forward vector
        Debug.Log($"SetForward called with forward vector: {forward}, newForward: {newForward}, magnitude: {newForward.magnitude}");

        if (newForward.sqrMagnitude > 0.0001f) // Check if the vector is not zero
        {
            if (model != null)
            {
                model.forward = newForward.normalized;
            }
            else
            {
                Debug.LogWarning("Model is null, cannot set forward direction.");
            }
        }
        else
        {
            Debug.LogWarning("Look rotation viewing vector is zero, skipping rotation.");
        }
    }

    public void MoveTile(Grid.Tile aTile)
    {
        if (myReachedTile && !aTile.occupied &&
            Grid.Instance.isReachable(myCurrentTile, aTile))
        {
            myCurrentTile = aTile;
        }
    }

    public void SetWalkBuffer(List<Grid.Tile> someTiles)
    {
        myWalkBuffer.Clear();
        myWalkBuffer.AddRange(someTiles);
        Debug.Log($"Walk buffer set with {myWalkBuffer.Count} tiles.");
    }
}
