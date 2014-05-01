using UnityEngine;
using System.Collections;

public class Swarmer : MonoBehaviour {
	public static readonly int KeyPositionsAroundTarget = 6;

    private HexGridManager _gridContainer = null;
	private NavMeshAgent _navAgent = null;
	private SwarmTarget _target = null;
    private bool needsDestination = true;
	
    private HexGridManager.Occupant _occupant = null;
    private HexGridManager.Reservation _reservation = null;
	
    HexGridManager.IntVector2 currentGrid = new HexGridManager.IntVector2();
    HexGridManager.IntVector2 tempGrid = new HexGridManager.IntVector2();
	
	// Use this for initialization
	void Start () {
		GameObject gridContainer = GameObject.FindGameObjectWithTag("GridContainer");
        _gridContainer = gridContainer.GetComponent< HexGridManager >();

		_navAgent = GetComponent< NavMeshAgent >();

		GameObject swarmGameObject = GameObject.FindGameObjectWithTag ("SwarmTarget");
		_target = swarmGameObject.GetComponent< SwarmTarget > ();

		_occupant = _gridContainer.CreateOccupant(gameObject, 1);
		
		_gridContainer.PositionToGrid(transform.position, ref currentGrid);
	}
	
	void OnDestroy() 
	{
		_gridContainer.ReturnOccupant(ref _occupant);
        _gridContainer.ReturnReservation(ref _reservation);
	}
	
	void FindSwarmDestination()
	{
        HexGridManager.IntVector2 targetGrid = _gridContainer.GetClosestVacantNeighbor(_target.gameObject, gameObject);
        Vector3 destination = Vector3.zero;
        _gridContainer.GridToPosition(ref targetGrid, ref destination);

        _navAgent.destination = destination;
        _reservation = _gridContainer.CreateReservation(destination);

        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        s.transform.position = destination;

        needsDestination = false;
	}
	
	// Update is called once per frame
	void Update () 
    {
		if ( needsDestination )
		{
			FindSwarmDestination();
		} 
		
		else
		{
			_gridContainer.PositionToGrid( transform.position, ref tempGrid );
			
			if( !tempGrid.Equals( ref currentGrid ) )
			{
                HexGridManager.IntVector2 grid = new HexGridManager.IntVector2();
                _gridContainer.PositionToGrid( _navAgent.destination, ref grid );
                if( _gridContainer.IsOccupied( ref grid, _reservation ) )
                {
                    _gridContainer.ReturnReservation(ref _reservation);
                    needsDestination = true;
                }

                _gridContainer.UpdateOccupant( _occupant, gameObject );
                currentGrid = tempGrid;
			}
			
		}
		
	}
}
