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
	
    private Vector3 currentGrid = Vector3.zero;
    private Vector3 tempGrid = Vector3.zero;
	
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
        Vector3 dir = ( _target.gameObject.transform.position - gameObject.transform.position ).normalized;
        _gridContainer.GetClosestVacantNeighbor(_target.gameObject, ref tempGrid, dir);
        Vector3 destination = Vector3.zero;
        _gridContainer.GridToPosition(tempGrid, ref destination);

        _navAgent.destination = destination;
        _reservation = _gridContainer.CreateReservation(destination);

        needsDestination = false;
	}
	
	// Update is called once per frame
	void Update () 
    {
		if ( needsDestination )
		{
			FindSwarmDestination();
		} 		
	}

    public void OnGridChanged()
    {
        if( _reservation != null && _gridContainer.IsOccupied( tempGrid, _reservation, _occupant ) )
        {
            _gridContainer.ReturnReservation(ref _reservation);
            needsDestination = true;
        }
    }
}
