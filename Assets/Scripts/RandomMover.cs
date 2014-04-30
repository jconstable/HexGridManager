using UnityEngine;
using System.Collections;

public class RandomMover : MonoBehaviour {

    private GridManager _gridContainer = null;
    private NavMeshAgent _navAgent = null;

    public Vector3 destination = new Vector3();

    private GridManager.Occupant _occupant = null;
    public GameObject debugPrefab = null;
    private GameObject debugObject = null;

    GridManager.IntVector2 currentGrid = new GridManager.IntVector2();
    GridManager.IntVector2 tempGrid = new GridManager.IntVector2();

    public bool roam = false;

	// Use this for initialization
	void Start () {
        GameObject gridContainer = GameObject.FindGameObjectWithTag("GridContainer");
        _gridContainer = gridContainer.GetComponent< GridManager >();
        _navAgent = GetComponent< NavMeshAgent >();

        if (debugPrefab)
        {
            debugObject = Instantiate( debugPrefab ) as GameObject;
            debugObject.transform.localPosition = new Vector3( );
        }

        _occupant = _gridContainer.CreateOccupant(gameObject, 1);

        _gridContainer.PositionToGrid(transform.position, ref currentGrid);
	}

    void OnDestroy() 
    {
        _gridContainer.ReturnOccupant(ref _occupant);
		Destroy (debugObject);
    }

    void GetNewDestination()
    {
        GridManager.IntVector2 vec = new GridManager.IntVector2();
        
        vec.Set(Random.Range(-70, 70), Random.Range(-70, 70));
        
        if (_gridContainer.IsValid(ref vec))
        {
            _gridContainer.GridToPosition( ref vec, ref destination );
            _navAgent.destination = destination;
            
            debugObject.transform.position = destination + (Vector3.up * 0.1f);
        }
    }
	
	// Update is called once per frame
	void Update () {

        float mag = _navAgent.velocity.magnitude;
        if ( mag == 0f && roam )
        {
            GetNewDestination();
        }
        else
        {
            _gridContainer.PositionToGrid( destination, ref tempGrid );

            if( mag < _navAgent.speed && _gridContainer.IsOccupied( ref tempGrid ) )
            {
                GetNewDestination();
            }

            _gridContainer.PositionToGrid( transform.position, ref tempGrid );

            if( !tempGrid.Equals( ref currentGrid ) )
            {
                currentGrid = tempGrid;
                _gridContainer.UpdateOccupant( _occupant, gameObject );
            }

        }
	    
	}
}
