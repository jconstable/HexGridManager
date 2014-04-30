using UnityEngine;
using System.Collections;

public class Swarmer : MonoBehaviour {
	public static readonly int KeyPositionsAroundTarget = 6;

	private GridManager _gridContainer = null;
	private NavMeshAgent _navAgent = null;
	private SwarmTarget _target = null;
    private bool needsDestination = true;
	
	private GridManager.Occupant _occupant = null;
	private GridManager.Reservation _reservation = null;
	
	GridManager.IntVector2 currentGrid = new GridManager.IntVector2();
	GridManager.IntVector2 tempGrid = new GridManager.IntVector2();
	
	// Use this for initialization
	void Start () {
		GameObject gridContainer = GameObject.FindGameObjectWithTag("GridContainer");
		_gridContainer = gridContainer.GetComponent< GridManager >();

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
		Vector3 dir = transform.position - _target.transform.position;
		dir.Normalize ();

		_target.swarmCount++;

        GridManager.IntVector2 grid = new GridManager.IntVector2();
        int magnitude = 1;
		while( needsDestination )
		{
            int rotations = KeyPositionsAroundTarget;
            float angleDelta = ( 360.0f / ( 2 * rotations ) );
            for( int i = 0; i < rotations; i++ )
            {
                float angle = angleDelta * i;
                if( _target.swarmCount % 2 == 0 ) angle = 360 - angle;
                Quaternion quat = Quaternion.AngleAxis( angle, Vector3.up );
                Vector3 tryDir = quat * dir;


                Vector3 tryPos = tryDir * magnitude * _gridContainer.GridSize;

                tryPos += _target.transform.position;

                _gridContainer.PositionToGrid( tryPos, ref grid );

                if( _gridContainer.IsValid( grid.x, grid.y ) && !_gridContainer.IsOccupied( grid.x, grid.y ) )
                {
                    _reservation = _gridContainer.CreateReservation( tryPos );
                    _navAgent.destination = tryPos;
                    needsDestination = false;
                    break;
                }
            }

            magnitude++;

            // Bail out if needed
            if( magnitude > 5 )
            {
                needsDestination = false;
                return;
            }
		}
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
				GridManager.IntVector2 grid = new GridManager.IntVector2();
                _gridContainer.PositionToGrid( _navAgent.destination, ref grid );
                if( _gridContainer.IsOccupied( grid.x, grid.y, _reservation ) )
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
