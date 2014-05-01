using UnityEngine;
using System.Collections;

public class SwarmTarget : MonoBehaviour {
	
    private HexGridManager _gridContainer = null;
	
    private HexGridManager.Occupant _occupant = null;

    public int swarmCount = 0;

	// Use this for initialization
	void Start () {
		GameObject gridContainer = GameObject.FindGameObjectWithTag("GridContainer");
        _gridContainer = gridContainer.GetComponent< HexGridManager >();
		_occupant = _gridContainer.CreateOccupant(gameObject, 2);
	}
	
	void OnDestroy() 
	{
		_gridContainer.ReturnOccupant(ref _occupant);
	}
}
