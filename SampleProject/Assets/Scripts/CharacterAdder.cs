using UnityEngine;
using System.Collections;

public class CharacterAdder : MonoBehaviour {
    public GameObject characterPrefab = null;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	    if (Input.GetMouseButtonUp(0) )
        {
            if( characterPrefab != null )
            {
                GameObject o = Instantiate( characterPrefab ) as GameObject;
                o.transform.position = Vector3.zero;
            }
        }
	}
}
