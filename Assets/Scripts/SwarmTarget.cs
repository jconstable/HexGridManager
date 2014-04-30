using UnityEngine;
using System.Collections;

public class SwarmTarget : MonoBehaviour {
	
	private GridManager _gridContainer = null;
	
	private GridManager.Occupant _occupant = null;

    public int swarmCount = 0;

	// Use this for initialization
	void Start () {
		GameObject gridContainer = GameObject.FindGameObjectWithTag("GridContainer");
		_gridContainer = gridContainer.GetComponent< GridManager >();
		_occupant = _gridContainer.CreateOccupant(gameObject, 2);
	}
	
	void OnDestroy() 
	{
		_gridContainer.ReturnOccupant(ref _occupant);
	}
}
